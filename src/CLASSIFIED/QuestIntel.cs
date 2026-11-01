using System.Collections.Generic;

namespace CLASSIFIED
{
    /// <summary>
    /// Intel about the player's own work: the tasks their profile has under way, read when the
    /// game picks the profile (and remembered for the next launch, so the terminal knows them
    /// before the profile loads). A notice now and then names one of them, from the faction's
    /// intelligence section, in the terminal's language; the task's name is the game's own.
    /// </summary>
    internal static class QuestIntel
    {
        private static readonly string[] English =
        {
            "RE: \"{quest}\". A SOURCE PUTS SCAV PATROLS ON THE OBJECTIVE TWICE A DAY. GO EARLY OR GO LATE.",
            "YOUR TASKING \"{quest}\" IS STILL OPEN. THE CLIENT ASKS FOR PROOF: PHOTOGRAPHS, NOT STORIES.",
            "INTERCEPTED TALK MENTIONS \"{quest}\". SOMEONE ELSE IS AFTER THE SAME THING. MOVE FIRST.",
            "\"{quest}\": {boss}'S PEOPLE HAVE BEEN SEEN NEAR {place}. EXPECT COMPANY ON THE WAY.",
            "ON \"{quest}\": THE AREA WAS QUIET LAST NIGHT. QUIET DOES NOT LAST HERE. TAKE THE WINDOW.",
            "\"{quest}\" IS MARKED PRIORITY BY COMMAND. RESUPPLY BEFORE YOU GO, NOT AFTER.",
            "A {enemy} TEAM WAS SEEN WORKING THE SAME GROUND AS \"{quest}\". DO NOT SHARE.",
            "STATUS CHECK ON \"{quest}\": STILL IN PROGRESS. COMMAND WOULD LIKE THAT TO CHANGE."
        };

        private static readonly string[] Russian =
        {
            "ПО ЗАДАЧЕ «{quest}»: ИСТОЧНИК СООБЩАЕТ О ПАТРУЛЯХ ДИКИХ У ОБЪЕКТА ДВАЖДЫ В СУТКИ. ВЫХОДИТЬ РАНО ИЛИ ПОЗДНО.",
            "ЗАДАЧА «{quest}» НЕ ВЫПОЛНЕНА. ЗАКАЗЧИК ТРЕБУЕТ ДОКАЗАТЕЛЬСТВ: ФОТОГРАФИЙ, А НЕ РАССКАЗОВ.",
            "В ПЕРЕХВАТЕ УПОМИНАЕТСЯ «{quest}». ЭТО ИЩЕТ КТО-ТО ЕЩЁ. ОПЕРЕДИТЬ.",
            "«{quest}»: ЛЮДЕЙ {boss} ВИДЕЛИ У {place}. НА МАРШРУТЕ ВОЗМОЖНА ВСТРЕЧА.",
            "ПО «{quest}»: НОЧЬЮ В РАЙОНЕ БЫЛО ТИХО. ТИШИНА ЗДЕСЬ НЕДОЛГО. ИСПОЛЬЗОВАТЬ ОКНО.",
            "ЗАДАЧА «{quest}» ПОСТАВЛЕНА ШТАБОМ НА КОНТРОЛЬ. ПОПОЛНИТЬ БОЕКОМПЛЕКТ ДО ВЫХОДА.",
            "ГРУППА {enemy} РАБОТАЕТ В ТОМ ЖЕ РАЙОНЕ, ЧТО И «{quest}». НЕ ДЕЛИТЬСЯ.",
            "ПРОВЕРКА ПО «{quest}»: ВСЁ ЕЩЁ В РАБОТЕ. ШТАБ ЖДЁТ ДОКЛАДА."
        };

        /// <summary>A notice about one of the tasks under way, or null if none are known.</summary>
        internal static Notice Make(System.Random random, Faction faction, float at)
        {
            List<string> quests = StartupTracker.Quests;
            if (quests.Count == 0)
            {
                return null;
            }
            bool russian = faction == Faction.Bear && Overlay.BearRussian;
            string[] lines = russian ? Russian : English;
            string quest = quests[random.Next(quests.Count)].ToUpperInvariant();
            return new Notice
            {
                At = at,
                Kind = NoticeKind.Intel,
                From = faction == Faction.Bear ? (russian ? "РАЗВЕДОТДЕЛ ШТАБА" : "HQ INTELLIGENCE") : "S2  //  TASKING",
                Body = Archive.PickLine("quest", new List<string>(lines), random).Replace("{quest}", quest)
            };
        }
    }
}
