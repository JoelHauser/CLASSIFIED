using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// The item browser, for checking the debrief's armoury: in the preview, K turns the debrief
    /// table over to everything it can ever draw (Kit.Catalogue), one item at a time, held until
    /// stepped on, each with its id and anything wrong with it. The load holds while it shows.
    ///
    ///   , / .  the item before or after. Shift: the weapon (all its builds) or the kind of gear
    ///   before or after. Home: back to the first. K: back to the usual table.
    ///
    /// Opening it logs every item with no model in the DLL or no figures in the data, so a broken
    /// one can be found without stepping through them all. The Item Browser setting opens the
    /// preview on it.
    /// </summary>
    internal partial class Overlay
    {
        private bool _browsing;

        /// <summary>Where the browser is in the catalogue: kept through a change of unit or layout, and from one preview to the next.</summary>
        private int _browseIndex;

        /// <summary>Gets the browser ready to show: the catalogue checked, the load held, a layout with room for the table.</summary>
        private void EnterBrowse()
        {
            _browsing = true;
            _previewHold = true;
            Kit.CheckCatalogue();
            // The dock leaves no room for the table, and a random layout might be it.
            if (_previewPins.Layout == LayoutKind.Random || _previewPins.Layout == LayoutKind.Dock)
            {
                _previewPins.Layout = LayoutKind.Split;
            }
        }

        private void ToggleBrowse()
        {
            if (_browsing)
            {
                _browsing = false;
                WeaponModel.ClearCache();
            }
            else
            {
                EnterBrowse();
                if (!AmbientRoom)
                {
                    _variant.Layout = _previewPins.Layout = LayoutKind.Split;
                }
            }
            // The table again, as the browser or as the unit's own.
            ShowLive(_variant);
        }

        /// <summary>The browser's keys, while it shows. True if one was pressed.</summary>
        private bool BrowseKeys()
        {
            if (!_browsing)
            {
                return false;
            }
            bool next = Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.PageDown);
            bool previous = Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.PageUp);
            string[] all = Kit.Catalogue();
            int to;
            if (Input.GetKeyDown(KeyCode.Home))
            {
                to = 0;
            }
            else if (next || previous)
            {
                bool group = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                to = group ? GroupStep(all, _browseIndex, next ? 1 : -1) : Wrap(_browseIndex + (next ? 1 : -1), all.Length);
            }
            else
            {
                return false;
            }
            _browseIndex = to;
            if (_ambient is Debrief debrief)
            {
                debrief.Browse(to);
            }
            return true;
        }

        /// <summary>The first item of the next weapon or kind of gear, or of this one's (going back from partway) or the one before.</summary>
        private static int GroupStep(string[] all, int from, int step)
        {
            string here = Kit.GroupOf(all[from]);
            if (step > 0)
            {
                int i = from;
                while (i < all.Length && Kit.GroupOf(all[i]) == here)
                {
                    i++;
                }
                return i < all.Length ? i : 0;
            }
            int start = from;
            while (start > 0 && Kit.GroupOf(all[start - 1]) == here)
            {
                start--;
            }
            if (start < from)
            {
                return start;
            }
            // Already at this one's first: the start of the one before.
            int j = Wrap(start - 1, all.Length);
            string before = Kit.GroupOf(all[j]);
            while (j > 0 && Kit.GroupOf(all[j - 1]) == before)
            {
                j--;
            }
            return j;
        }

        private const string BrowseHelp =
            "ITEM BROWSER      , / .  ITEM      SHIFT + , / .  WEAPON OR KIND      HOME  FIRST      K  LEAVE      LEFT/RIGHT  UNIT      UP/DOWN  LAYOUT\n"
            + "SPACE  HOLD      - / +  SPEED      V  VIEW      H  HIDE THIS      ESC  CLOSE      PROBLEMS ARE LOGGED TO BepInEx\\LogOutput.log";
    }
}
