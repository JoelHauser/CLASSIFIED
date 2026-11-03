using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// Which section of the faction's network the terminal signs in to. Random picks one of the
    /// player's own faction per launch; picking one shows it every launch, whichever the faction.
    /// </summary>
    public enum ThemeChoice
    {
        Random,
        BearCommand,
        BearReconnaissance,
        BearSignals,
        BearCounterintelligence,
        BearSupply,
        BearMedical,
        UsecOperations,
        UsecIntelligence,
        UsecSignals,
        UsecLogistics,
        UsecMedical,
        UsecCommand
    }

    /// <summary>Which faction's terminal shows. Profile follows the selected profile's faction.</summary>
    public enum FactionChoice
    {
        Profile,
        Bear,
        Usec
    }

    internal enum Faction
    {
        Unknown,
        Bear,
        Usec
    }

    /// <summary>How often the terminal glitches.</summary>
    public enum GlitchLevel
    {
        Off,
        Subtle,
        Heavy
    }

    // The parts of the screen. Random is for the settings only: it means "let the unit
    // choose", and a rolled launch never holds it. Each faction draws them its own way: the
    // backdrops are BEAR's or USEC's own pictures, the frames and bars come in a set for each.
    public enum BackdropKind { Random, Map, Imagery, Document, Plain }

    /// <summary>Reticle, Gauge and Plate are BEAR's; Hud, Scope and Lock USEC's.</summary>
    public enum FrameKind { Random, Reticle, Gauge, Plate, Hud, Scope, Lock }
    /// <summary>Stamp is BEAR's, Boot USEC's; the rest either side's.</summary>
    public enum IntroKind { Random, Fade, TitleCard, Wipe, Crt, Sheen, Doors, Boot }
    /// <summary>Lamps and Tape are BEAR's; Ruler and Segments USEC's.</summary>
    public enum BarKind { Random, Lamps, Tape, Ruler, Segments }
    public enum OutroKind { Random, Zoom, Lift, Sting, Slide, Flash }
    public enum LayoutKind { Random, Centered, Split, SplitMirror, Sidebar, Dock }

    /// <summary>Parts fixed by the settings (or the preview) instead of rolled.</summary>
    internal struct Pins
    {
        internal LayoutKind Layout;
        internal BackdropKind Backdrop;
        internal FrameKind Frame;
        internal IntroKind Intro;
        internal BarKind Bar;
        internal OutroKind Outro;

        internal static Pins FromConfig() => new Pins
        {
            Layout = ClassifiedPlugin.Layout.Value,
            Backdrop = ClassifiedPlugin.Backdrop.Value,
            Frame = ClassifiedPlugin.Frame.Value,
            Intro = ClassifiedPlugin.Intro.Value,
            Bar = ClassifiedPlugin.Bar.Value,
            Outro = ClassifiedPlugin.Ending.Value
        };
    }

    /// <summary>
    /// One unit's look and voice: palette, the choices for each of the five parts of the screen
    /// (backdrop, insignia frame, intro, progress bar, ending), and the words it uses. A launch
    /// picks one of each from the unit's lists.
    /// </summary>
    internal sealed class Theme
    {
        internal readonly ThemeChoice Id;
        internal readonly Faction Faction;
        internal readonly Color32 Ink;
        internal readonly Color32 Glow;
        internal readonly Color32 Primary;
        internal readonly Color32 Accent;
        internal readonly Color32 Text;
        internal readonly Color32 Dim;
        internal readonly Color32 Faint;

        /// <summary>Slow steps and AUTHENTICATION FAILED.</summary>
        internal readonly Color32 Warn;

        /// <summary>The second colour of the RGB split, opposite the accent.</summary>
        internal readonly Color32 Split;

        internal readonly string AccentHex;
        internal readonly string WarnHex;

        internal BackdropKind[] Backdrops;
        internal FrameKind[] Frames;
        internal IntroKind[] Intros;
        internal BarKind[] Bars;
        internal OutroKind[] Outros;
        internal bool Scanlines;

        /// <summary>The unit's own name, in Latin letters: memo senders, the preview. The subtitle's comes from UnitNames.</summary>
        internal string Division;

        /// <summary>BEAR units' own name in Russian, shown beside the Latin one where the font has the letters.</summary>
        internal string Native;
        internal string LogTag = "OK";
        internal string SlowNote = "STAND BY";
        internal string[] Headers;
        internal string[] Grants;
        internal string[] Chatter;
        internal string[] Headlines;

        /// <summary>The unit's own orders; may hold Lore placeholders like {nick} or {boss}.</summary>
        internal string[] Memos;

        /// <summary>BEAR, USEC, or CLASSIFIED before the faction is known: the wordmark.</summary>
        internal string Wordmark => Faction == Faction.Bear ? "BEAR" : Faction == Faction.Usec ? "USEC" : "CLASSIFIED";

        private Theme(ThemeChoice id, Faction faction, string ink, string glow, string primary,
            string accent, string text, string dim, string warn, string split)
        {
            Id = id;
            Faction = faction;
            Ink = Hex(ink);
            Glow = Hex(glow);
            Primary = Hex(primary);
            Accent = Hex(accent);
            Text = Hex(text);
            Dim = Hex(dim);
            Faint = WithAlpha(Dim, 60);
            Warn = Hex(warn);
            Split = Hex(split);
            AccentHex = "#" + accent;
            WarnHex = "#" + warn;
        }

        internal static readonly Theme[] All =
        {
            // ---- BEAR -------------------------------------------------------------------
            new Theme(ThemeChoice.BearCommand, Faction.Bear, "0A0303", "5A0E0E", "C4262A", "E8C35A", "F4E9D8", "A08A78", "FF7A3C", "3CC8FF")
            {
                Backdrops = new[] { BackdropKind.Map, BackdropKind.Document }, Frames = new[] { FrameKind.Plate, FrameKind.Gauge },
                Intros = new[] { IntroKind.TitleCard, IntroKind.Crt }, Bars = new[] { BarKind.Lamps }, Outros = new[] { OutroKind.Sting, OutroKind.Zoom },
                Division = "COMMAND POST", Native = "ШТАБ", LogTag = "ВЫП",
                SlowNote = "ЖДАТЬ ПРИКАЗА",
                Headers = new[] { "BEAR  //  ШТАБ ГРУППИРОВКИ  //  СОВЕРШЕННО СЕКРЕТНО", "BEAR  //  КОМАНДНЫЙ ПУНКТ  //  ОСОБОЙ ВАЖНОСТИ", "BEAR  //  ОПЕРАТИВНЫЙ ОТДЕЛ" },
                Grants = new[] { "ПРИКАЗ ПОЛУЧЕН", "ДОСТУП РАЗРЕШЁН" },
                Chatter = new[] { "ПРИКАЗ {n}  ОТДАН", "ДЕЖУРНЫЙ  НА МЕСТЕ", "КАРТА ОБСТАНОВКИ  ОБНОВЛЕНА", "ПОВЕРКА  {n} В СТРОЮ" },
                Headlines = new[] { "COMMAND CONFIRMS OPERATION WILL CONTINUE", "REINFORCEMENTS 'EXPECTED SHORTLY', SAYS COMMAND" },
                Memos = new[] { "THE OPERATION CONTINUES. NO UNIT IS TO WITHDRAW WITHOUT A WRITTEN ORDER FROM THIS HEADQUARTERS.", "SQUAD LEADERS WILL REPORT STRENGTH AND AMMUNITION BY 0600 DAILY. 'ENOUGH' IS NOT A NUMBER." }
            },
            new Theme(ThemeChoice.BearReconnaissance, Faction.Bear, "060803", "2E3A12", "7E9A2E", "C8E07A", "EEF5DC", "8E9A72", "FF8A3C", "C83CFF")
            {
                Backdrops = new[] { BackdropKind.Imagery, BackdropKind.Map }, Frames = new[] { FrameKind.Reticle, FrameKind.Gauge },
                Intros = new[] { IntroKind.Crt, IntroKind.TitleCard }, Bars = new[] { BarKind.Tape, BarKind.Lamps }, Outros = new[] { OutroKind.Slide, OutroKind.Lift },
                Division = "RECONNAISSANCE", Native = "РАЗВЕДКА", LogTag = "НАБЛ",
                SlowNote = "ДЕРЖАТЬ ПОЗИЦИЮ",
                Headers = new[] { "BEAR  //  РАЗВЕДКА  //  СЕКРЕТНО", "BEAR  //  РАЗВЕДЫВАТЕЛЬНЫЙ ПУНКТ  //  СЕКРЕТНО", "BEAR  //  ОТДЕЛЬНЫЙ РАЗВЕДВЗВОД" },
                Grants = new[] { "НАБЛЮДАТЕЛЬ НА МЕСТЕ", "ЦЕЛЬ ВИЖУ" },
                Chatter = new[] { "НП {n}  ДОКЛАДЫВАЕТ", "БПЛА  ЗАРЯД {pct}%", "СНИМКИ {hex}  ПОЛУЧЕНЫ", "МАРШРУТ ДОЗОРА  ЗАПИСАН" },
                Headlines = new[] { "RECON TEAM RETURNS WITH FULL REPORT AND NO RATIONS", "NEW OBSERVATION POST 'HAS A GOOD VIEW', SAYS SPOTTER" },
                Memos = new[] { "OBSERVATION POSTS WILL BE RELIEVED EVERY SIX HOURS. AN OBSERVER WHO FALLS ASLEEP WILL BE RELIEVED PERMANENTLY.", "PHOTOGRAPHS ARE TO SHOW THE TARGET. PHOTOGRAPHS OF SUNSETS OVER THE BAY WILL BE DELETED." }
            },
            new Theme(ThemeChoice.BearSignals, Faction.Bear, "0A0600", "5A3200", "FFA020", "FFD27A", "FFF0D2", "B08A50", "FF4632", "3CA0FF")
            {
                Backdrops = new[] { BackdropKind.Map, BackdropKind.Plain }, Frames = new[] { FrameKind.Gauge, FrameKind.Reticle },
                Intros = new[] { IntroKind.Crt }, Bars = new[] { BarKind.Lamps }, Outros = new[] { OutroKind.Zoom, OutroKind.Flash },
                Scanlines = true,
                Division = "SIGNALS", Native = "СВЯЗЬ", LogTag = "ПРИН",
                SlowNote = "СЛАБЫЙ СИГНАЛ",
                Headers = new[] { "BEAR  //  УЗЕЛ СВЯЗИ  //  СЕКРЕТНО", "BEAR  //  УЗЕЛ СВЯЗИ  //  ДЛЯ СЛУЖЕБНОГО ПОЛЬЗОВАНИЯ", "BEAR  //  ЗАСЕКРЕЧЕННАЯ СВЯЗЬ" },
                Grants = new[] { "КАНАЛ ЗАКРЫТ", "СВЯЗЬ УСТАНОВЛЕНА" },
                Chatter = new[] { "КАНАЛ {n}  ЧИСТ", "СМЕНА КЛЮЧА  В 00:00", "ПЕРЕХВАТ {hex}  ЗАПИСАН", "РЕТРАНСЛЯТОР  {pct}% МОЩНОСТИ" },
                Headlines = new[] { "SIGNALS RESTORES CONTACT WITH ALL UNITS EXCEPT THE ONES IT CANNOT FIND", "NEW CODEBOOK ISSUED. OLD CODEBOOK BURNED, MOSTLY" },
                Memos = new[] { "RADIO DISCIPLINE: TRANSMISSIONS UNDER TEN SECONDS. THE ENEMY IS LISTENING, AND SO IS THE COLONEL.", "TODAY'S CALL SIGNS ARE IN THE SEALED ENVELOPE. DO NOT USE YESTERDAY'S. DO NOT USE YOUR NAME." }
            },
            new Theme(ThemeChoice.BearCounterintelligence, Faction.Bear, "08060A", "3A1420", "A8303C", "E0A0A8", "F0E6EA", "9A8890", "FFC83C", "3CFFC8")
            {
                Backdrops = new[] { BackdropKind.Document, BackdropKind.Imagery }, Frames = new[] { FrameKind.Plate, FrameKind.Reticle },
                Intros = new[] { IntroKind.TitleCard, IntroKind.Crt }, Bars = new[] { BarKind.Tape }, Outros = new[] { OutroKind.Sting, OutroKind.Lift },
                Division = "COUNTERINTELLIGENCE", Native = "КОНТРРАЗВЕДКА", LogTag = "ПРОВ",
                SlowNote = "НА ПРОВЕРКЕ",
                Headers = new[] { "BEAR  //  ОСОБЫЙ ОТДЕЛ  //  СОВЕРШЕННО СЕКРЕТНО", "BEAR  //  ОСОБЫЙ ОТДЕЛ  //  ЛИЧНО В РУКИ", "BEAR  //  КОНТРРАЗВЕДКА" },
                Grants = new[] { "БЛАГОНАДЁЖНОСТЬ ПОДТВЕРЖДЕНА", "ЛИЧНОСТЬ УСТАНОВЛЕНА" },
                Chatter = new[] { "ДЕЛО {hex}  ЗАВЕДЕНО", "ДОПРОС {n}  ЗАПИСАН", "ЛИЧНЫЙ НОМЕР {emp}  ПРОВЕРЕН", "ПОЧТА  ПРОСМОТРЕНА" },
                Headlines = new[] { "COUNTERINTELLIGENCE FINDS NO LEAKS. FINDING ITSELF LEAKED", "ALL PERSONNEL TO BE REVIEWED. REVIEWERS ALSO TO BE REVIEWED" },
                Memos = new[] { "ANY CONTACT WITH USEC PERSONNEL, ARMED OR OTHERWISE, IS TO BE REPORTED TO THIS OFFICE WITHIN THE HOUR.", "THE FLEA MARKET IS AN ENEMY INTELLIGENCE SOURCE. WHAT YOU SELL THERE TELLS THEM WHAT YOU HAVE." }
            },
            new Theme(ThemeChoice.BearSupply, Faction.Bear, "0A0804", "4A3A1A", "B89A5A", "E8D4A0", "F8F0DE", "A89878", "FF5A3C", "4A8CFF")
            {
                Backdrops = new[] { BackdropKind.Document, BackdropKind.Map }, Frames = new[] { FrameKind.Gauge, FrameKind.Plate },
                Intros = new[] { IntroKind.Crt, IntroKind.TitleCard }, Bars = new[] { BarKind.Lamps, BarKind.Tape }, Outros = new[] { OutroKind.Slide, OutroKind.Lift },
                Division = "SUPPLY", Native = "ТЫЛ", LogTag = "ВЫД",
                SlowNote = "КОЛОННА ЗАДЕРЖИВАЕТСЯ",
                Headers = new[] { "BEAR  //  СЛУЖБА ТЫЛА  //  ДСП", "BEAR  //  ТЫЛ  //  ДЛЯ СЛУЖЕБНОГО ПОЛЬЗОВАНИЯ", "BEAR  //  ВЕЩЕВОЙ СКЛАД" },
                Grants = new[] { "ЗАЯВКА УТВЕРЖДЕНА", "ПОЛУЧЕНО ПОД РОСПИСЬ" },
                Chatter = new[] { "ЯЩИК {hex}  ПЕРЕСЧИТАН", "СУХПАЙ  НА {n} СУТОК", "ГОРЮЧЕЕ  {pct}%", "ЗАЯВКА {n}  ОТКЛОНЕНА" },
                Headlines = new[] { "TUSHONKA STOCKS 'ADEQUATE', SAYS QUARTERMASTER WHO WILL NOT OPEN THE STORE", "SUPPLY CONVOY ARRIVES WITH WRONG CALIBRE AGAIN" },
                Memos = new[] { "AMMUNITION IS ISSUED, NOT GIVEN. EVERY ROUND IS SIGNED FOR, AND EVERY EMPTY CASE IS ACCOUNTED FOR.", "THE QUARTERMASTER IS AWARE THE BOOTS ARE THE WRONG SIZE. THE FEET WILL BE ADJUSTED." }
            },
            new Theme(ThemeChoice.BearMedical, Faction.Bear, "05080A", "1A3A44", "5AB4C8", "B8E8F2", "F0FBFF", "7EA0A8", "FF4A4A", "FFB43C")
            {
                Backdrops = new[] { BackdropKind.Map, BackdropKind.Document }, Frames = new[] { FrameKind.Plate, FrameKind.Gauge },
                Intros = new[] { IntroKind.TitleCard, IntroKind.Crt }, Bars = new[] { BarKind.Tape, BarKind.Lamps }, Outros = new[] { OutroKind.Zoom, OutroKind.Sting },
                Division = "MEDICAL SERVICE", Native = "МЕДСЛУЖБА", LogTag = "СТАБ",
                SlowNote = "ИДЁТ СОРТИРОВКА",
                Headers = new[] { "BEAR  //  МЕДИЦИНСКИЙ ПУНКТ", "BEAR  //  МЕДИЦИНСКАЯ СЛУЖБА  //  ДЛЯ СЛУЖЕБНОГО ПОЛЬЗОВАНИЯ", "BEAR  //  МЕДСАНБАТ" },
                Grants = new[] { "ГОДЕН К СЛУЖБЕ", "ДОПУЩЕН МЕДКОМИССИЕЙ" },
                Chatter = new[] { "КОЙКА {n}  СВОБОДНА", "ЗАПАС КРОВИ  {pct}%", "РАНЕНЫЙ {emp}  СТАБИЛЕН", "АПТЕЧКИ  {n} ШТ" },
                Headlines = new[] { "FIELD HOSPITAL REPORTS RECORD RECOVERIES AND RECORD ARRIVALS", "MEDICS ASK OPERATORS TO STOP USING PAINKILLERS AS BREAKFAST" },
                Memos = new[] { "STIMULANTS FROM UNKNOWN SOURCES ARE NOT TO BE INJECTED. THIS INCLUDES ANYTHING BOUGHT FROM SANITAR.", "TOURNIQUETS ARE FOR LIMBS. THEY ARE NOT A SUBSTITUTE FOR A SLING, A BELT, OR A HANDLE." }
            },

            // ---- USEC -------------------------------------------------------------------
            new Theme(ThemeChoice.UsecOperations, Faction.Usec, "03060C", "0E2A4A", "2E86DE", "8CC8FF", "E6F2FF", "6E88A6", "FFAA33", "FF3B5C")
            {
                Backdrops = new[] { BackdropKind.Map, BackdropKind.Imagery }, Frames = new[] { FrameKind.Hud, FrameKind.Lock },
                Intros = new[] { IntroKind.Boot, IntroKind.Crt }, Bars = new[] { BarKind.Ruler, BarKind.Segments }, Outros = new[] { OutroKind.Slide, OutroKind.Zoom },
                Division = "TACTICAL OPERATIONS CENTER", LogTag = "COMPLETE",
                SlowNote = "STAND BY",
                Headers = new[] { "USEC  //  TOC  //  SECRET//REL USEC", "USEC  //  TACTICAL OPERATIONS CENTER", "USEC  //  OPERATIONS  //  SECRET" },
                Grants = new[] { "AUTHENTICATED", "YOU ARE CLEARED" },
                Chatter = new[] { "FRAGO {n}  PUBLISHED", "BATTLE TRACK  UPDATED", "QRF  {n} MIN OUT", "WATCH OFFICER  ON DECK" },
                Headlines = new[] { "TOC REPORTS ALL TEAMS ACCOUNTED FOR, MOSTLY", "COMMAND APPROVES EXTENSION OF OPERATION 'UNTIL FURTHER NOTICE'" },
                Memos = new[] { "ALL TEAMS CHECK IN ON THE HOUR. A TEAM THAT MISSES TWO CHECK-INS IS LISTED MISSING AND ITS GEAR IS ALLOCATED.", "THE PLAN HAS CHANGED. THE NEW PLAN IS THE OLD PLAN WITH FEWER PEOPLE. ACKNOWLEDGE BY 1800." }
            },
            new Theme(ThemeChoice.UsecIntelligence, Faction.Usec, "06050C", "2A2050", "7A6AD8", "C4BCFF", "EEEAFF", "8A84AE", "FFBE3C", "3CFFC8")
            {
                Backdrops = new[] { BackdropKind.Imagery, BackdropKind.Document }, Frames = new[] { FrameKind.Scope, FrameKind.Lock },
                Intros = new[] { IntroKind.Boot, IntroKind.TitleCard }, Bars = new[] { BarKind.Ruler }, Outros = new[] { OutroKind.Sting, OutroKind.Lift },
                Division = "S2 INTELLIGENCE", LogTag = "ASSESSED",
                SlowNote = "AWAITING CONFIRMATION",
                Headers = new[] { "USEC  //  S2  //  SECRET//NOFORN", "USEC  //  INTELLIGENCE SECTION", "USEC  //  S2  //  ANALYST WORKSTATION" },
                Grants = new[] { "BRIEFING READY", "NEED TO KNOW CONFIRMED" },
                Chatter = new[] { "INTSUM {n}  RELEASED", "SOURCE {hex}  GRADED B2", "IMAGERY  {n} FRAMES", "THREAT BOARD  REVISED" },
                Headlines = new[] { "S2 ASSESSES ENEMY STRENGTH AS 'MORE THAN WE WOULD LIKE'", "ANALYSTS CONFIRM THE BOSS IS WHERE THE BOSS USUALLY IS" },
                Memos = new[] { "CONFIDENCE LEVELS ARE TO BE STATED IN EVERY REPORT. 'PRETTY SURE' IS NOT A CONFIDENCE LEVEL.", "INTELLIGENCE FOLDERS RECOVERED IN THE FIELD COME TO S2 FIRST. THEY ARE NOT CURRENCY." }
            },
            new Theme(ThemeChoice.UsecSignals, Faction.Usec, "020906", "0A4430", "2EC48A", "9CF5CC", "E6FFF4", "6EA890", "FF8C3C", "FF3CA0")
            {
                Backdrops = new[] { BackdropKind.Map, BackdropKind.Plain }, Frames = new[] { FrameKind.Hud, FrameKind.Scope },
                Intros = new[] { IntroKind.Crt, IntroKind.Boot }, Bars = new[] { BarKind.Segments }, Outros = new[] { OutroKind.Flash, OutroKind.Zoom },
                Scanlines = true,
                Division = "S6 COMMUNICATIONS", LogTag = "ROGER",
                SlowNote = "LINK DEGRADED",
                Headers = new[] { "USEC  //  S6  //  COMSEC", "USEC  //  COMMUNICATIONS SECTION", "USEC  //  S6  //  NET CONTROL" },
                Grants = new[] { "NET JOINED", "COMMS CHECK, LOUD AND CLEAR" },
                Chatter = new[] { "NET {n}  UP", "CRYPTO FILL  LOADED", "REPEATER {hex}  ONLINE", "SATCOM WINDOW  {n} MIN" },
                Headlines = new[] { "S6 RESTORES UPLINK; TEAMS ASKED TO STOP STREAMING MUSIC OVER IT", "JAMMING OVER THE CITY 'NOT OURS', CONFIRMS S6" },
                Memos = new[] { "RADIOS ARE TO BE CHECKED BEFORE EVERY DEPARTURE. A RADIO THAT WORKED YESTERDAY IS NOT A CHECKED RADIO.", "DO NOT KEY THE MIC TO SAY 'ROGER' TWICE. ONCE WAS ENOUGH. THE FIRST TIME WAS ENOUGH." }
            },
            new Theme(ThemeChoice.UsecLogistics, Faction.Usec, "0A0805", "4A3820", "C8A064", "F0D8A8", "FFF6E6", "AE9C80", "FF5A32", "4A8CFF")
            {
                Backdrops = new[] { BackdropKind.Map, BackdropKind.Document }, Frames = new[] { FrameKind.Lock, FrameKind.Hud },
                Intros = new[] { IntroKind.Boot, IntroKind.Crt }, Bars = new[] { BarKind.Segments, BarKind.Ruler }, Outros = new[] { OutroKind.Lift, OutroKind.Slide },
                Division = "S4 LOGISTICS", LogTag = "STAGED",
                SlowNote = "AWAITING RESUPPLY",
                Headers = new[] { "USEC  //  S4  //  SUPPLY CONTROL", "USEC  //  LOGISTICS SECTION", "USEC  //  S4  //  FORWARD STOCK" },
                Grants = new[] { "RESUPPLY CONFIRMED", "DRAW APPROVED" },
                Chatter = new[] { "PALLET {hex}  STAGED", "CLASS V  {pct}%", "VEHICLE {n}  FUELLED", "BACKORDER  {n} ITEMS" },
                Headlines = new[] { "RESUPPLY FLIGHT CANCELLED. RESUPPLY BY FOOT 'UNDER CONSIDERATION'", "S4 LOCATES MISSING PALLET IN A SCAV CAMP" },
                Memos = new[] { "ALL SALVAGED EQUIPMENT IS USEC PROPERTY UNTIL S4 SAYS OTHERWISE. S4 RARELY SAYS OTHERWISE.", "MRES ARE NOT TO BE TRADED WITH LOCALS. THE EXCHANGE RATE IS BEING MONITORED." }
            },
            new Theme(ThemeChoice.UsecMedical, Faction.Usec, "0A0506", "4A1418", "D8404A", "FFB2B8", "FFF0F2", "B88890", "FFC850", "50D2FF")
            {
                Backdrops = new[] { BackdropKind.Document, BackdropKind.Map }, Frames = new[] { FrameKind.Scope, FrameKind.Hud },
                Intros = new[] { IntroKind.TitleCard, IntroKind.Boot }, Bars = new[] { BarKind.Ruler }, Outros = new[] { OutroKind.Zoom, OutroKind.Sting },
                Division = "MEDICAL // CASEVAC", LogTag = "STABLE",
                SlowNote = "TRIAGE IN PROGRESS",
                Headers = new[] { "USEC  //  ROLE 1 AID STATION", "USEC  //  MEDICAL  //  CASEVAC COORDINATION", "USEC  //  MEDICAL SECTION" },
                Grants = new[] { "FIT FOR DUTY", "MEDICAL CLEARANCE GRANTED" },
                Chatter = new[] { "LITTER {n}  READY", "WHOLE BLOOD  {pct}%", "CASEVAC {hex}  COMPLETE", "9-LINE  RECEIVED" },
                Headlines = new[] { "AID STATION TREATS RECORD NUMBER OF 'I'M FINE' INJURIES", "MEDICS REMIND TEAMS THAT BLACKED-OUT LIMBS ARE NOT A STRATEGY" },
                Memos = new[] { "EVERY OPERATOR CARRIES TWO TOURNIQUETS. ONE FOR YOU, ONE FOR WHOEVER FORGOT THEIRS.", "PAINKILLERS MASK INJURIES. THEY DO NOT FIX THEM. A FRACTURE IS STILL A FRACTURE AT THE EXTRACT." }
            },
            new Theme(ThemeChoice.UsecCommand, Faction.Usec, "080705", "4A3C14", "D8B040", "FFE08A", "FFF6DA", "AA9A70", "FF5A3C", "5A8CFF")
            {
                Backdrops = new[] { BackdropKind.Document, BackdropKind.Imagery }, Frames = new[] { FrameKind.Lock, FrameKind.Scope },
                Intros = new[] { IntroKind.TitleCard, IntroKind.Boot }, Bars = new[] { BarKind.Segments }, Outros = new[] { OutroKind.Sting, OutroKind.Flash },
                Division = "CORPORATE COMMAND", LogTag = "APPROVED",
                SlowNote = "PENDING APPROVAL",
                Headers = new[] { "USEC  //  CORPORATE COMMAND  //  CONFIDENTIAL", "USEC INTERNATIONAL  //  EXECUTIVE NETWORK", "USEC  //  CONTRACT MANAGEMENT" },
                Grants = new[] { "WELCOME, CONTRACTOR", "CONTRACT ACTIVE" },
                Chatter = new[] { "INVOICE {n}  DISPUTED", "CONTRACT CLAUSE {n}  REVIEWED", "HAZARD PAY  {pct}% APPROVED", "BOARD CALL  RESCHEDULED" },
                Headlines = new[] { "USEC REMAINS 'FULLY COMMITTED' TO CLIENT, SAYS CLIENT-FREE PRESS OFFICE", "CONTRACT DISPUTE WITH TERRAGROUP ENTERS THIRD YEAR" },
                Memos = new[] { "THE TERRAGROUP CONTRACT IS SUSPENDED, NOT ENDED. YOU ARE STILL BOUND BY ITS CONFIDENTIALITY CLAUSES.", "HAZARD PAY IS CALCULATED FROM THE MOMENT YOU REACH THE EXTRACT. ARRIVE AT THE EXTRACT." }
            }
        };

        internal static Theme Get(ThemeChoice id)
        {
            foreach (Theme theme in All)
            {
                if (theme.Id == id)
                {
                    return theme;
                }
            }
            return All[0];
        }

        /// <summary>The units a launch can roll for a faction.</summary>
        internal static List<Theme> UnitsOf(Faction faction)
        {
            var units = new List<Theme>();
            foreach (Theme theme in All)
            {
                if (theme.Faction == faction && faction != Faction.Unknown)
                {
                    units.Add(theme);
                }
            }
            return units;
        }

        internal static Faction ParseSide(string side) =>
            side == "BEAR" ? Faction.Bear : side == "USEC" ? Faction.Usec : Faction.Unknown;

        internal static Faction Opposite(Faction faction) =>
            faction == Faction.Bear ? Faction.Usec : faction == Faction.Usec ? Faction.Bear : Faction.Unknown;

        internal static string Name(Faction faction) =>
            faction == Faction.Bear ? "BEAR" : faction == Faction.Usec ? "USEC" : "UNKNOWN";

        internal static Color32 WithAlpha(Color32 color, byte alpha) => new Color32(color.r, color.g, color.b, alpha);

        private static Color32 Hex(string hex) => new Color32(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16),
            255);
    }

    internal enum NoticeKind
    {
        Memo,
        Intel,

        /// <summary>The other faction's radio traffic, picked up by ours.</summary>
        Intercept,
        Note
    }

    /// <summary>One notice that slides in during the load. Its body may hold Lore placeholders, filled as it shows.</summary>
    internal sealed class Notice
    {
        internal float At;
        internal NoticeKind Kind;
        internal string From;
        internal string Body;
        /// <summary>For a memo: who signed it and its reference, shown under the body.</summary>
        internal string Signature;
    }

    /// <summary>
    /// Everything about one launch's terminal that is left to chance: unit, ring layout,
    /// header, badge, the closing words, the news ticker, and the rare surprises (a fake denial,
    /// a memo). Rolled once when the terminal starts, so nothing here costs anything per frame.
    /// </summary>
    internal sealed class Variant
    {
        internal Theme Theme;
        internal GlitchLevel Glitches;

        internal BackdropKind Backdrop;
        internal FrameKind Frame;
        internal IntroKind Intro;
        internal BarKind Bar;
        internal OutroKind Outro;

        /// <summary>How the brand and the readout are arranged on the screen.</summary>
        internal LayoutKind Layout;

        internal float DashSpin;
        internal float ArcSpin;
        internal int ChartSeed;

        internal string Header;

        /// <summary>What the unit calls itself this launch, its motto, and its words for the stages (UnitNames).</summary>
        internal UnitNames.Pair Name;
        internal UnitNames.Pair Motto;
        internal UnitNames.Pair Boot;
        internal UnitNames.Pair SignIn;
        internal UnitNames.Pair Deploy;
        internal UnitNames.Pair Return;
        internal string TopRight;
        internal string GrantText;
        internal bool Greet;
        internal bool FakeDenial;
        internal bool Milestone;
        internal string Ticker;

        /// <summary>The notices that slide in during the load, in order: memos, intel, the odd note.</summary>
        internal Notice[] Notices;

        /// <summary>The faction this terminal belongs to; Unknown before a profile has ever been seen.</summary>
        internal Faction Faction => Theme.Faction;

        /// <summary>
        /// Takes the look of <paramref name="from"/>: its unit, its name and motto, parts, rings,
        /// header and badge, so a later load in the session is the terminal the player signed in
        /// to. The words (the notices, the ticker, the closing line) stay this roll's own.
        /// </summary>
        internal void KeepLook(Variant from)
        {
            Theme = from.Theme;
            Backdrop = from.Backdrop;
            Frame = from.Frame;
            Intro = from.Intro;
            Bar = from.Bar;
            Outro = from.Outro;
            Layout = from.Layout;
            DashSpin = from.DashSpin;
            ArcSpin = from.ArcSpin;
            ChartSeed = from.ChartSeed;
            Header = from.Header;
            Name = from.Name;
            Motto = from.Motto;
            Boot = from.Boot;
            SignIn = from.SignIn;
            Deploy = from.Deploy;
            Return = from.Return;
            TopRight = from.TopRight;
            Milestone = from.Milestone;
        }

        /// <param name="preview">
        /// A preview (or a roll never shown): shows three notices, is more likely to fake a
        /// denial, and does not mark what it picks as seen.
        /// </param>
        /// <param name="faction">Whose terminal: the player's faction, or Unknown to sign in unidentified.</param>
        internal static Variant Roll(System.Random random, ThemeChoice choice, Faction faction, GlitchLevel glitches, int launches, Pins pins, bool preview = false)
        {
            var v = new Variant { Glitches = glitches };
            // What this launch shows is picked from what the player has not seen lately; a
            // preview picks the same way but leaves the record alone.
            Archive.NewRun();
            Archive.Recording = !preview;
            v.Milestone = launches > 0 && launches % 100 == 0;

            if (faction == Faction.Unknown)
            {
                // Nothing known about the operator yet (a first launch): either side, until the
                // profile loads and puts it right.
                faction = random.Next(2) == 0 ? Faction.Bear : Faction.Usec;
            }
            if (choice != ThemeChoice.Random)
            {
                v.Theme = Theme.Get(choice);
            }
            else if (v.Milestone && faction != Faction.Unknown)
            {
                v.Theme = Theme.Get(faction == Faction.Bear ? ThemeChoice.BearCommand : ThemeChoice.UsecCommand);
            }
            else
            {
                // Every unit of the faction equally likely, but the ones shown in the last few
                // launches sit out, so the same screen does not come round again soon.
                List<Theme> units = Theme.UnitsOf(faction);
                var names = new List<string>();
                foreach (Theme theme in units)
                {
                    names.Add(theme.Id.ToString());
                }
                v.Theme = units[Archive.PickShuffled("unit." + faction, names, random)];
            }
            Theme t = v.Theme;
            v.Backdrop = PickPart(random, t, "backdrop", t.Backdrops);
            v.Frame = PickPart(random, t, "frame", t.Frames);
            v.Intro = PickPart(random, t, "intro", t.Intros);
            v.Bar = PickPart(random, t, "bar", t.Bars);
            v.Outro = PickPart(random, t, "outro", t.Outros);
            v.Layout = PickLayout(random);
            if (pins.Layout != LayoutKind.Random) v.Layout = pins.Layout;
            if (pins.Backdrop != BackdropKind.Random) v.Backdrop = pins.Backdrop;
            if (pins.Frame != FrameKind.Random) v.Frame = pins.Frame;
            if (pins.Intro != IntroKind.Random) v.Intro = pins.Intro;
            if (pins.Bar != BarKind.Random) v.Bar = pins.Bar;
            if (pins.Outro != OutroKind.Random) v.Outro = pins.Outro;

            v.DashSpin = Range(random, 6f, 16f) * Sign(random);
            v.ArcSpin = Range(random, 16f, 36f) * Sign(random);
            v.ChartSeed = random.Next();
            UnitNames.Pair[] unitNames = UnitNames.Names(choice);
            v.Name = unitNames[random.Next(unitNames.Length)];
            UnitNames.Pair[] mottos = UnitNames.Mottos(choice);
            v.Motto = mottos[random.Next(mottos.Length)];
            v.Boot = UnitNames.Booting[random.Next(UnitNames.Booting.Length)];
            v.SignIn = UnitNames.SigningIn[random.Next(UnitNames.SigningIn.Length)];
            v.Deploy = UnitNames.Deploying[random.Next(UnitNames.Deploying.Length)];
            v.Return = UnitNames.Returning[random.Next(UnitNames.Returning.Length)];

            v.Header = Lang.Bear(t.Headers[random.Next(t.Headers.Length)]);
            string service = t.Faction == Faction.Bear ? $"{(Overlay.BearRussian ? "АБВГДЕКМНТ" : "ABVGDEKMNT")[random.Next(10)]}-{random.Next(100000, 1000000)}"
                : $"{random.Next(100, 1000)}-{random.Next(10, 100)}-{random.Next(1000, 10000)}";
            if (t.Faction == Faction.Bear && Overlay.BearRussian)
            {
                // A Soviet-style clearance: the form of admission, one to three.
                string form = "123"[random.Next(3)].ToString();
                string seans = launches > 0 ? $"  //  СЕАНС {launches:0000}" : string.Empty;
                v.TopRight = $"ЛИЧНЫЙ НОМЕР {service}  //  ДОПУСК ПО ФОРМЕ {form}{seans}{(v.Milestone ? "  //  ЮБИЛЕЙ" : string.Empty)}";
            }
            else
            {
                string clearance = t.Faction == Faction.Unknown ? "PENDING" : "ABCD"[random.Next(4)].ToString();
                string session = launches > 0 ? $"  //  SESSION {launches:0000}" : string.Empty;
                v.TopRight = $"SERVICE NO {service}  //  CLEARANCE {clearance}{session}{(v.Milestone ? "  //  MILESTONE" : string.Empty)}";
            }

            // CLEARANCE CONFIRMED is the mod's own line: it stays the most common ending.
            v.GrantText = random.Next(100) < 50 ? ClearanceLine(t.Faction) : Lang.Bear(t.Grants[random.Next(t.Grants.Length)]);
            v.Greet = random.Next(100) < 30;
            float denial = glitches == GlitchLevel.Off ? 0f : preview ? 1f / 3f : glitches == GlitchLevel.Heavy ? 1f / 8f : 1f / 25f;
            v.FakeDenial = random.NextDouble() < denial;

            v.Ticker = BuildTicker(random, t);

            // The traffic, spread over the load: the unit's orders, orders from elsewhere in the
            // faction, regional intel, the enemy's radio traffic, and now and then something
            // nobody admits to sending. Plenty of it: five on a launch, six over a raid's long
            // load, three on the way back from one. They queue, one at a time, so a load that
            // ends early just leaves the rest unread. A preview shows three, to look at.
            int count = preview ? 3
                : StartupTracker.Kind == RunKind.Deploy ? 6
                : StartupTracker.Kind == RunKind.Return ? 3
                : 5;
            var at = new float[count];
            for (int i = 0; i < count; i++)
            {
                at[i] = count == 1 ? 0.45f : 0.08f + 0.74f * i / (count - 1);
            }
            v.Notices = new Notice[count];
            List<string> divisionMemos = Correspondence.MemosFor(t);
            string[][] common = Correspondence.CommonMemos(t.Faction);
            var commonBodies = new List<string>();
            foreach (string[] memo in common)
            {
                commonBodies.Add(memo[1]);
            }
            Faction enemy = Theme.Opposite(t.Faction);
            for (int i = 0; i < count; i++)
            {
                var notice = new Notice { At = at[i] + Range(random, 0f, 0.08f) };
                int roll = random.Next(100);
                if (roll < 6)
                {
                    notice.Kind = NoticeKind.Note;
                    notice.From = "UNKNOWN SENDER";
                    notice.Body = Lore.EasterEgg(random);
                }
                else if (roll < 20 && enemy != Faction.Unknown)
                {
                    notice.Kind = NoticeKind.Intercept;
                    notice.From = Theme.Name(enemy) + " NET";
                    notice.Body = Archive.PickLine("intercept", Correspondence.Intercepts(enemy), random);
                }
                else if (roll < 45)
                {
                    notice.Kind = NoticeKind.Intel;
                    notice.From = Lore.IntelSender(random);
                    notice.Body = Lore.IntelTemplate(random);
                }
                else if (random.Next(10) < 7 || commonBodies.Count == 0)
                {
                    notice.Kind = NoticeKind.Memo;
                    notice.From = t.Faction == Faction.Bear && Overlay.BearRussian && !string.IsNullOrEmpty(t.Native) ? t.Native : t.Division;
                    notice.Body = Archive.PickLine("memo", divisionMemos, random);
                    notice.Signature = Correspondence.Signature(random, t.Id);
                }
                else
                {
                    int pick = Archive.Pick("memo", commonBodies, random);
                    string sender = common[pick][0];
                    notice.Kind = NoticeKind.Memo;
                    notice.From = sender;
                    notice.Body = commonBodies[pick];
                    notice.Signature = Correspondence.Signature(random, t.Id, sender);
                }
                v.Notices[i] = notice;
            }
            // Now and then one of them is about the operator's own tasks, when they are known.
            if (!preview && count > 0 && StartupTracker.Quests.Count > 0 && random.Next(100) < 60)
            {
                Notice quest = QuestIntel.Make(random, t.Faction, v.Notices[count - 1].At);
                if (quest != null)
                {
                    v.Notices[count - 1] = quest;
                }
            }
            Archive.Save();
            return v;
        }

        /// <summary>CLEARANCE CONFIRMED, the mod's own closing line, in the faction's language.</summary>
        internal static string ClearanceLine(Faction faction) => faction == Faction.Bear && Overlay.BearRussian ? "ДОПУСК ПОДТВЕРЖДЁН" : "CLEARANCE CONFIRMED";

        /// <summary>
        /// One loop of the ticker: the day's readings between headlines. The headlines are the
        /// unit's and the faction's, the ones the player has not seen lately, with a few lines of
        /// regional intel among them.
        /// </summary>
        private static string BuildTicker(System.Random random, Theme theme)
        {
            const int Count = 8;
            List<string> pool = Correspondence.HeadlinesFor(theme);
            var headlines = new List<string>();
            int intel = random.Next(2, 4);
            for (int i = 0; i < Count - intel; i++)
            {
                headlines.Add(Lore.Fill(Archive.PickLine("headline", pool, random), random));
            }
            headlines.AddRange(Lore.Headlines(random, intel));
            for (int i = headlines.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (headlines[i], headlines[j]) = (headlines[j], headlines[i]);
            }

            // Between headlines, a line of the day's readings: radio, weather, readiness.
            var builder = new StringBuilder();
            for (int h = 0; h < headlines.Count; h++)
            {
                builder.Append(Reading(random, theme)).Append("     |     ");
                builder.Append(headlines[h]).Append("     |     ");
            }
            return builder.ToString();
        }

        private static readonly string[] Bearings = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>One reading for the ticker, with its value coloured good or bad; BEAR's in Russian.</summary>
        private static string Reading(System.Random random, Theme theme)
        {
            string good = theme.AccentHex;
            string bad = theme.WarnHex;
            bool ok = random.Next(100) < 70;
            bool bear = theme.Faction == Faction.Bear && Overlay.BearRussian;
            string Value(string yes, string no) => $"<color={(ok ? good : bad)}>{(ok ? yes : no)}</color>";
            switch (random.Next(7))
            {
                case 0:
                    return bear
                        ? $"ЧАСТОТА {random.Next(30, 88)},{random.Next(0, 1000):000}  {Value("ЧИСТО", "ПОМЕХИ")}"
                        : $"FREQ {random.Next(30, 88)}.{random.Next(0, 1000):000}  {Value("CLEAR", "JAMMED")}";
                case 1:
                    return bear
                        ? $"ВЕТЕР {BearingsRu[random.Next(BearingsRu.Length)]} {random.Next(1, 14)} М/С"
                        : $"WIND {Bearings[random.Next(Bearings.Length)]} {random.Next(1, 14)} M/S";
                case 2:
                    return bear
                        ? $"ВИДИМОСТЬ {random.Next(1, 12)} КМ  //  ТЕМП. {random.Next(-18, 22)} С"
                        : $"VIS {random.Next(1, 12)} KM  //  TEMP {random.Next(-18, 22)} C";
                case 3:
                    return bear
                        ? $"БОЕВАЯ ГОТОВНОСТЬ  {Value("ПОСТОЯННАЯ", "ПОЛНАЯ")}"
                        : $"FPCON  {Value("BRAVO", "CHARLIE")}";
                case 4:
                    return bear
                        ? $"РЕЗЕРВ  {Value(random.Next(4, 15) + " МИН", "ЗАДЕЙСТВОВАН")}"
                        : $"QRF  {Value(random.Next(4, 15) + " MIN", "COMMITTED")}";
                case 5:
                    return bear
                        ? $"ЛУНА {random.Next(0, 101)}%  //  ЗАКАТ {random.Next(16, 21)}:{random.Next(0, 60):00}"
                        : $"MOON {random.Next(0, 101)}%  //  SUNSET {random.Next(16, 21)}:{random.Next(0, 60):00}";
                default:
                    return bear
                        ? $"ЭВАКУАЦИЯ  {Value("ВОЗМОЖНА", "НЕЛЁТНАЯ ПОГОДА")}"
                        : $"CASEVAC  {Value("AVAILABLE", "GROUNDED")}";
            }
        }

        private static readonly string[] BearingsRu = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };

        /// <summary>One of the unit's choices for a slot, avoiding the ones it showed last time.</summary>
        private static T PickPart<T>(System.Random random, Theme theme, string slot, T[] choices)
        {
            var names = new List<string>();
            foreach (T choice in choices)
            {
                names.Add(choice.ToString());
            }
            return choices[Archive.PickShuffled(slot + "." + theme.Id, names, random)];
        }

        private static LayoutKind PickLayout(System.Random random)
        {
            // Any layout, avoiding the ones shown last time, like the units.
            var layouts = new List<LayoutKind>();
            var names = new List<string>();
            foreach (LayoutKind layout in Enum.GetValues(typeof(LayoutKind)))
            {
                if (layout != LayoutKind.Random)
                {
                    layouts.Add(layout);
                    names.Add(layout.ToString());
                }
            }
            return layouts[Archive.PickShuffled("layout", names, random)];
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static float Sign(System.Random random) => random.Next(2) == 0 ? -1f : 1f;
    }

    /// <summary>
    /// Made-up radio chatter for the line under the log: the unit's own lines and shared
    /// ones, picked from what the player has not seen lately (see Archive). Never mixed into the
    /// real steps, so the log and its times stay true.
    /// </summary>
    internal sealed class Chatter
    {
        private readonly System.Random _random;
        private readonly List<string> _division;
        private readonly string[] _common;

        internal Chatter(System.Random random, Theme theme)
        {
            _random = random;
            _division = Correspondence.ChatterFor(theme);
            _common = Correspondence.CommonChatter(theme.Faction);
        }

        internal string Next()
        {
            // The network watches the whole region: about a third of the chatter is intel. (The
            // unsigned notes are too long for this one line; they arrive as notices instead.)
            int roll = _random.Next(100);
            if (roll < 35)
            {
                return ":: " + Lore.IntelLine(_random);
            }
            // The unit's own lines come up more often than the shared ones.
            string line = Lang.Bear(roll < 75 && _division.Count > 0
                ? Archive.PickLine("chatter", _division, _random)
                : Archive.PickLine("chatter", _common, _random));
            if (line.IndexOf('{') < 0)
            {
                return ":: " + line;
            }
            return ":: " + line
                .Replace("{hex}", _random.Next(0x1000, 0x10000).ToString("X4"))
                .Replace("{mods}", BepInEx.Bootstrap.Chainloader.PluginInfos.Count.ToString())
                .Replace("{emp}", $"{_random.Next(1, 100):00}-{_random.Next(1000, 10000)}")
                .Replace("{pct}", _random.Next(40, 100).ToString())
                .Replace("{n}", _random.Next(2, 999).ToString());
        }
    }
}
