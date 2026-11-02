namespace CLASSIFIED
{
    /// <summary>
    /// What each unit calls itself under the faction name, its motto on the title card, and the
    /// words for the stage of the launch: several of each, one picked per launch, so the same
    /// unit does not introduce itself the same way twice running. BEAR's come in Russian with
    /// the English for the BEAR Language setting.
    /// </summary>
    internal static class UnitNames
    {
        internal struct Pair
        {
            internal string Ru;
            internal string En;

            internal Pair(string ru, string en)
            {
                Ru = ru;
                En = en;
            }
        }

        private static Pair P(string ru, string en) => new Pair(ru, en);

        private static Pair E(string en) => new Pair(en, en);

        /// <summary>The unit's names: its own first, then the others it goes by.</summary>
        internal static Pair[] Names(ThemeChoice unit)
        {
            switch (unit)
            {
                case ThemeChoice.BearCommand: return new[] { P("ШТАБ", "COMMAND POST"), P("КОМАНДНЫЙ ПУНКТ", "COMMAND POST"), P("ШТАБ ГРУППИРОВКИ", "GROUP HEADQUARTERS"), P("ОПЕРАТИВНЫЙ ОТДЕЛ", "OPERATIONS DEPARTMENT"), P("ЦЕНТР БОЕВОГО УПРАВЛЕНИЯ", "COMBAT CONTROL CENTRE") };
                case ThemeChoice.BearReconnaissance: return new[] { P("РАЗВЕДКА", "RECONNAISSANCE"), P("РАЗВЕДВЗВОД", "RECON PLATOON"), P("ГРУППА СПЕЦНАЗА", "SPECIAL PURPOSE GROUP"), P("НАБЛЮДАТЕЛЬНЫЙ ПУНКТ", "OBSERVATION POST"), P("РАЗВЕДДОЗОР", "RECON PATROL") };
                case ThemeChoice.BearSignals: return new[] { P("СВЯЗЬ", "SIGNALS"), P("УЗЕЛ СВЯЗИ", "SIGNALS NODE"), P("РОТА СВЯЗИ", "SIGNALS COMPANY"), P("РАДИОРАЗВЕДКА", "RADIO INTELLIGENCE"), P("ЗАСЕКРЕЧЕННАЯ СВЯЗЬ", "SECURE COMMUNICATIONS") };
                case ThemeChoice.BearCounterintelligence: return new[] { P("КОНТРРАЗВЕДКА", "COUNTERINTELLIGENCE"), P("ОСОБЫЙ ОТДЕЛ", "SPECIAL DEPARTMENT"), P("ОТДЕЛ БЕЗОПАСНОСТИ", "SECURITY DEPARTMENT"), P("ГРУППА ПРОВЕРКИ", "VETTING SECTION") };
                case ThemeChoice.BearSupply: return new[] { P("ТЫЛ", "SUPPLY"), P("СЛУЖБА ТЫЛА", "REAR SERVICES"), P("ВЕЩЕВОЙ СКЛАД", "QUARTERMASTER STORES"), P("АВТОКОЛОННА", "TRANSPORT COLUMN"), P("СКЛАД ГСМ", "FUEL DEPOT") };
                case ThemeChoice.BearMedical: return new[] { P("МЕДСЛУЖБА", "MEDICAL SERVICE"), P("МЕДПУНКТ", "AID POST"), P("МЕДСАНБАТ", "MEDICAL BATTALION"), P("ЭВАКОГРУППА", "EVACUATION TEAM") };
                case ThemeChoice.UsecOperations: return new[] { E("TACTICAL OPERATIONS CENTER"), E("S3 OPERATIONS"), E("CURRENT OPERATIONS"), E("BATTLE WATCH"), E("JOINT OPERATIONS CELL") };
                case ThemeChoice.UsecIntelligence: return new[] { E("S2 INTELLIGENCE"), E("ALL-SOURCE ANALYSIS"), E("TARGETING CELL"), E("ISR COORDINATION"), E("HUMINT SECTION") };
                case ThemeChoice.UsecSignals: return new[] { E("S6 COMMUNICATIONS"), E("NETWORK OPERATIONS"), E("SIGNAL SECTION"), E("COMSEC CUSTODIAN"), E("RETRANS TEAM") };
                case ThemeChoice.UsecLogistics: return new[] { E("S4 LOGISTICS"), E("SUPPORT OPERATIONS"), E("FORWARD SUPPLY POINT"), E("MOTOR POOL"), E("MOVEMENT CONTROL") };
                case ThemeChoice.UsecMedical: return new[] { E("MEDICAL // CASEVAC"), E("ROLE 1 AID STATION"), E("FORWARD SURGICAL TEAM"), E("PATIENT MOVEMENT"), E("COMBAT MEDIC SECTION") };
                case ThemeChoice.UsecCommand: return new[] { E("CORPORATE COMMAND"), E("CONTRACT OVERSIGHT"), E("EXECUTIVE OPERATIONS"), E("REGIONAL DIRECTORATE"), E("CLIENT RELATIONS") };
                default: return new[] { E("") };
            }
        }

        /// <summary>The title card's line under the faction name.</summary>
        internal static Pair[] Mottos(ThemeChoice unit)
        {
            switch (unit)
            {
                case ThemeChoice.BearCommand: return new[] { P("BATTLE ENCOUNTER ASSAULT REGIMENT", "BATTLE ENCOUNTER ASSAULT REGIMENT"), P("ПРИКАЗ ЕСТЬ ПРИКАЗ", "AN ORDER IS AN ORDER"), P("НИ ШАГУ НАЗАД", "NOT ONE STEP BACK"), P("ВПЕРЁД, И ТОЛЬКО ВПЕРЁД", "FORWARD, ONLY FORWARD") };
                case ThemeChoice.BearReconnaissance: return new[] { P("ВИЖУ ВСЁ", "I SEE EVERYTHING"), P("ВИДЕТЬ, НЕ БУДУЧИ ЗАМЕЧЕННЫМ", "TO SEE AND NOT BE SEEN"), P("ГЛАЗА И УШИ", "EYES AND EARS"), P("ТИШЕ ВОДЫ, НИЖЕ ТРАВЫ", "QUIETER THAN WATER, LOWER THAN GRASS") };
                case ThemeChoice.BearSignals: return new[] { P("СВЯЗЬ ЕСТЬ", "WE HAVE COMMS"), P("БЕЗ СВЯЗИ НЕТ УПРАВЛЕНИЯ", "NO COMMS, NO COMMAND"), P("ПРИЁМ", "OVER"), P("СЛЫШУ ВАС ХОРОШО", "READING YOU LOUD AND CLEAR") };
                case ThemeChoice.BearCounterintelligence: return new[] { P("ДОВЕРЯЙ, НО ПРОВЕРЯЙ", "TRUST, BUT VERIFY"), P("БОЛТУН — НАХОДКА ДЛЯ ШПИОНА", "A CHATTERBOX IS A GIFT TO A SPY"), P("ВСЁ ЗАПИСАНО", "EVERYTHING IS ON RECORD"), P("НЕ БОЛТАЙ", "DON'T TALK") };
                case ThemeChoice.BearSupply: return new[] { P("БЕЗ ТЫЛА НЕТ ФРОНТА", "NO REAR, NO FRONT"), P("ВСЁ ДЛЯ ФРОНТА", "EVERYTHING FOR THE FRONT"), P("ПАТРОНЫ, ХЛЕБ, СОЛЯРКА", "ROUNDS, BREAD, DIESEL"), P("СЧЁТ ВЕДЁТСЯ", "IT IS ALL COUNTED") };
                case ThemeChoice.BearMedical: return new[] { P("СВОИХ НЕ БРОСАЕМ", "WE DON'T LEAVE OUR OWN"), P("ЗОЛОТОЙ ЧАС", "THE GOLDEN HOUR"), P("ЖИВЫМ — ДОМОЙ", "HOME ALIVE"), P("СНАЧАЛА ЖГУТ", "TOURNIQUET FIRST") };
                case ThemeChoice.UsecOperations: return new[] { E("UNITED SECURITY"), E("ONE TEAM, ONE MISSION"), E("PLAN, BRIEF, EXECUTE"), E("SPEED, SURPRISE, VIOLENCE OF ACTION") };
                case ThemeChoice.UsecIntelligence: return new[] { E("KNOW BEFORE YOU GO"), E("ALWAYS OUT FRONT"), E("THE ENEMY GETS A VOTE"), E("TRUST THE PATTERN") };
                case ThemeChoice.UsecSignals: return new[] { E("SAY AGAIN, OVER"), E("SHOOT, MOVE, COMMUNICATE"), E("NO COMMS, NO BOMBS"), E("LOUD AND CLEAR") };
                case ThemeChoice.UsecLogistics: return new[] { E("BEANS, BULLETS, BANDAGES"), E("AMATEURS TALK TACTICS"), E("NOTHING HAPPENS UNTIL SOMETHING MOVES"), E("ON TIME, ON TARGET") };
                case ThemeChoice.UsecMedical: return new[] { E("SO THAT OTHERS MAY LIVE"), E("THE GOLDEN HOUR"), E("NO ONE LEFT BEHIND"), E("STOP THE BLEED") };
                case ThemeChoice.UsecCommand: return new[] { E("UNITED SECURITY INTERNATIONAL"), E("CONTRACT FIRST"), E("DISCRETION GUARANTEED"), E("SECURITY IS OUR BUSINESS") };
                default: return new[] { E("") };
            }
        }

        /// <summary>The launch's stage before the profile server answers.</summary>
        internal static readonly Pair[] Booting =
        {
            P("ЗАПУСК СИСТЕМЫ", "STARTING UP"), P("ВКЛЮЧЕНИЕ", "POWERING ON"), P("САМОКОНТРОЛЬ", "SELF-TEST"),
            P("ХОЛОДНЫЙ ЗАПУСК", "COLD START"), P("ИНИЦИАЛИЗАЦИЯ", "INITIALISING"), P("ПРОВЕРКА СИСТЕМ", "SYSTEMS CHECK")
        };

        /// <summary>The launch's stage once the profile server has answered.</summary>
        internal static readonly Pair[] SigningIn =
        {
            P("ВХОД В СЕТЬ", "SIGNING IN"), P("ПРОВЕРКА ДОПУСКА", "VERIFYING CLEARANCE"), P("ОПОЗНАВАНИЕ", "AUTHENTICATING"),
            P("СИНХРОНИЗАЦИЯ", "SYNCHRONISING"), P("УСТАНОВКА СВЯЗИ", "ESTABLISHING LINK"), P("ДОКЛАД ПРИНЯТ", "REPORTING IN")
        };

        /// <summary>The raid load's stage.</summary>
        internal static readonly Pair[] Deploying =
        {
            P("ВЫДВИЖЕНИЕ", "DEPLOYING"), P("ВЫХОД НА ЗАДАНИЕ", "MOVING OUT"), P("НА МАРШРУТЕ", "EN ROUTE"),
            P("ПРОНИКНОВЕНИЕ", "INFILTRATING"), P("ВЫСАДКА", "INSERTION")
        };
    }
}
