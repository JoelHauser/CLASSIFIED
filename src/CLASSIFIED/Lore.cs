using System.Collections.Generic;

namespace CLASSIFIED
{
    /// <summary>
    /// What the faction's network knows about the region: where the bosses were last seen,
    /// which enemy teams are moving where, what the traders are buying, what is selling on the
    /// flea. Fills the chatter, the ticker and the priority intel notices, so the terminal reads
    /// like your side watching Tarkov while you wait. Plus the odd note nobody signed.
    /// </summary>
    internal static class Lore
    {
        private struct Spot
        {
            internal string Map;
            internal string[] Places;
        }

        private static readonly Spot[] Maps =
        {
            new Spot { Map = "CUSTOMS", Places = new[] { "DORMS", "BIG RED", "THE FORTRESS", "NEW GAS", "CONSTRUCTION" } },
            new Spot { Map = "FACTORY", Places = new[] { "OFFICE BLOCK", "GATE 3", "PUMPING STATION", "THE TUNNELS" } },
            new Spot { Map = "WOODS", Places = new[] { "SAWMILL", "USEC CAMP", "SCAV BUNKER", "THE CRASH SITE" } },
            new Spot { Map = "SHORELINE", Places = new[] { "THE RESORT", "THE PIER", "POWER STATION", "WEATHER STATION" } },
            new Spot { Map = "INTERCHANGE", Places = new[] { "ULTRA MALL", "IDEA", "GOSHAN", "KIBA ARMS" } },
            new Spot { Map = "RESERVE", Places = new[] { "THE BUNKERS", "BLACK PAWN", "WHITE KING", "THE DRAINS" } },
            new Spot { Map = "LIGHTHOUSE", Places = new[] { "WATER TREATMENT", "THE CHALETS", "ROGUE CAMP", "THE ISLAND" } },
            new Spot { Map = "STREETS", Places = new[] { "LEXOS", "CONCORDIA", "PINEWOOD HOTEL", "CHEKANNAYA 15" } },
            new Spot { Map = "THE LAB", Places = new[] { "THE SERVER ROOM", "THE MANAGER'S OFFICE", "THE ARENA", "BLUE LAB" } },
            new Spot { Map = "GROUND ZERO", Places = new[] { "TERRAGROUP HQ", "EMERCOM", "THE UNDERPASS", "NAKATANI" } }
        };

        private static readonly string[][] Bosses =
        {
            new[] { "RESHALA", "CUSTOMS", "DORMS" },
            new[] { "KILLA", "INTERCHANGE", "ULTRA MALL" },
            new[] { "SHTURMAN", "WOODS", "SAWMILL" },
            new[] { "GLUKHAR", "RESERVE", "THE BARRACKS" },
            new[] { "SANITAR", "SHORELINE", "THE RESORT" },
            new[] { "TAGILLA", "FACTORY", "THE FORGE" },
            new[] { "KABAN", "STREETS", "THE CAR DEALERSHIP" },
            new[] { "KOLLONTAY", "STREETS", "THE POLICE ACADEMY" },
            new[] { "ZRYACHIY", "LIGHTHOUSE", "THE ISLAND" },
            new[] { "THE GOONS", "LIGHTHOUSE", "THE CHALETS" },
            new[] { "PARTISAN", "WOODS", "THE TREELINE" },
            new[] { "CULTISTS", "CUSTOMS", "THE GAS STATION" }
        };

        private static readonly string[] Traders = { "PRAPOR", "THERAPIST", "FENCE", "SKIER", "PEACEKEEPER", "MECHANIC", "RAGMAN", "JAEGER", "REF" };

        private static readonly string[] Items =
        {
            "LEDX", "GRAPHICS CARDS", "PHYSICAL BITCOIN", "LABS KEYCARDS", "MOONSHINE", "INTELLIGENCE FOLDERS",
            "TETRIZ CONSOLES", "THERMAL SCOPES", "GOLDEN ROOSTERS", "RED KEYCARDS", "SALEWA KITS", "GAS ANALYZERS"
        };

        private static readonly string[] Intel =
        {
            "{boss} SIGHTED  //  {bossmap}  //  {bossplace}",
            "{boss} CONFIRMED ACTIVE  //  {bossmap}  //  AVOID {bossplace}",
            "BEAR SQUAD ({n4}) MOVING THROUGH {place}, {map}",
            "USEC PATROL ENGAGED SCAVS NEAR {place}, {map}",
            "SCAV ACTIVITY UP {pct}% AT {place}, {map}",
            "{trader} RECEIVED A SHIPMENT OF {item}",
            "{trader} BUYING {item} ABOVE MARKET",
            "FENCE OFFERING {item} AT A {pct}% MARKUP",
            "FLEA MARKET: {item} UP {pct}% SINCE MORNING",
            "FLEA MARKET: {item} DOWN {pct}% ON NEWS FROM {map}",
            "AIRDROP INBOUND  //  {map}",
            "BTR ROUTE CHANGED  //  {map}",
            "EXFIL AT {place} REPORTED COMPROMISED",
            "PMC CASUALTIES TODAY: {n3}. SCAV CASUALTIES: UNCOUNTED",
            "{faction} OPERATOR {nick} LOGGED ENTERING THE REGION",
            "SIGNAL INTERCEPTED FROM {place}, {map}. DECRYPTING",
            "LIGHTKEEPER: NO CONTACT FOR {n2} DAYS",
            "DRONE FOOTAGE OF {place} UNDER REVIEW",
            "UNIDENTIFIED SNIPER AT {place}, {map}",
            "RAIDERS DEPLOYED TO {place}, {map}",
            "{boss} NOT SEEN AT {bossplace} FOR {n2} HOURS. LOCATION UNKNOWN",
            "{boss} GUARDS COUNTED: {n4}. WELL EQUIPPED",
            "HEAVY GUNFIRE REPORTED AT {place}, {map}. NO FRIENDLY TEAMS IN AREA",
            "{enemy} TEAM ({n4}) SEEN MOVING THROUGH {place}, {map}",
            "{enemy} OVERWATCH SUSPECTED AT {place}, {map}",
            "{enemy} CASUALTIES CONFIRMED AT {place}. BODIES ALREADY LOOTED",
            "{enemy} RADIO TRAFFIC UP {pct}% OVER {map}",
            "{faction} TEAM REQUESTS SUPPORT AT {place}, {map}",
            "SMOKE OBSERVED OVER {place}, {map}",
            "{faction} SQUAD EXTRACTED VIA {place}, {map} WITH HEAVY LOAD",
            "ROGUE PATROL EXPANDED PAST {place}, LIGHTHOUSE",
            "CULTIST ACTIVITY AFTER DARK AT {place}, {map}",
            "MINEFIELD MARKERS MOVED NEAR {place}, {map}",
            "{trader} HAS STOPPED TAKING ORDERS FOR {item}",
            "{trader} IS ASKING ABOUT A MISSING SHIPMENT. AVOID",
            "{trader} PRICES UP {pct}% OVERNIGHT",
            "FENCE BOUGHT {n3} ITEMS FROM SCAVS TODAY. NO QUESTIONS ASKED",
            "RAGMAN RESTOCKED BODY ARMOUR. QUEUE FORMING",
            "MECHANIC SEEN AT THE FACTORY. MOOD: POOR",
            "PEACEKEEPER RAISED HIS RATES. AGAIN",
            "JAEGER POSTED A NEW TASK NEAR {place}, {map}",
            "SKIER'S MEN ARE COLLECTING DEBTS IN {map}",
            "THERAPIST REQUESTING SURGICAL SUPPLIES. PAYING WELL",
            "REF HAS OPENED THE ARENA FOR ANOTHER ROUND",
            "FLEA MARKET LISTING OF {item} REMOVED AFTER {n2} MINUTES",
            "FLEA MARKET FEES UP {pct}%. TRADERS UNHAPPY",
            "PRICE OF {item} DOUBLED AFTER {boss} SIGHTING",
            "AIRDROP AT {place}, {map} CONTESTED BY {n4} GROUPS",
            "BTR SEEN IDLING AT {place}, STREETS",
            "EXFIL CAR AT {place}, {map} DEMANDING HIGHER FARE",
            "POWER RESTORED TO {place}, {map}. EXTRACTS MAY BE LIT",
            "CO-OP EXTRACT AT {place} ACTIVE. SCAVS COOPERATIVE, FOR NOW",
            "BEAR CASUALTIES IN {map}: {n3}. USEC CASUALTIES: {n3}",
            "USEC CONVOY SPOTTED LEAVING {place}, {map}",
            "BEAR CHECKPOINT SET UP NEAR {place}, {map}",
            "{n2} PMC OPERATORS CONFIRMED MISSING IN {map}",
            "SCAV BOSS CHANGE AT {bossplace}. NEW LEADER UNKNOWN",
            "SIGNAL JAMMING DETECTED OVER {map}",
            "LABS KEYCARD SOLD FOR {n3}K ROUBLES",
            "ARMOURED VEHICLE ABANDONED AT {place}, {map}",
            "FLARE SEEN OVER {place}, {map} AT DUSK",
            "THE GOONS LAST SEEN HEADING TOWARDS {map}",
            "PARTISAN TRAPS FOUND NEAR {place}, {map}",
            "SANITAR SEEN TRADING STIMULANTS NEAR {place}, SHORELINE",
            "WEATHER WORSENING OVER {map}. VISIBILITY POOR",
            "UNKNOWN AIRCRAFT OVER {map}. NOT OURS. NOT THEIRS EITHER",
            "GROUND ZERO PERIMETER BREACHED AT {place}"
        };

        private static readonly string[] EasterEggs =
        {
            "TAGILLA HAS ASKED, THROUGH AN INTERMEDIARY, FOR HIS HAMMER BACK. NOBODY HERE HAS HIS HAMMER. NOBODY HERE WANTS IT.",
            "THE BTR DRIVER IS NOT TO BE CALLED 'UNCLE' ON THE RADIO. HE CAN HEAR YOU. HE IS FLATTERED, AND IT IS A PROBLEM.",
            "A PARACORD LISTING ON THE FLEA MARKET AT 1 ROUBLE IS NOT A SUPPLY DROP. DO NOT BUY IT.",
            "MECHANIC HAS CONFIRMED GUNSMITH TASK 16 IS STILL OPEN. HE WILL NOT SAY WHEN IT CLOSES. HE SMILED WHEN WE ASKED.",
            "NOBODY KNOWS WHAT IS IN SANITAR'S INJECTORS. THE PEOPLE WHO FOUND OUT ARE NOT AVAILABLE FOR COMMENT.",
            "THE LIGHTKEEPER IS NOT TAKING VISITORS. THE LAST VISITOR IS ALSO NOT TAKING VISITORS.",
            "KILLA'S HELMET HAS BEEN SEEN ON THE FLEA MARKET. KILLA HAS BEEN SEEN LOOKING FOR WHOEVER SOLD IT.",
            "IF YOU HEAR 'CHEEKI BREEKI', TAKE COVER. IF YOU HEAR IT TWICE, TAKE BETTER COVER.",
            "THERE IS NO WIPE COMING. STOP PLANNING AROUND ONE. STOP SELLING YOUR STASH TO FENCE 'JUST IN CASE'.",
            "THE GOLDEN ROOSTER IN THE HIDEOUT IS NOT A MASCOT. IT IS COLLATERAL.",
            "PRAPOR'S AMMUNITION COUNT AND OURS HAVE NEVER MATCHED. WE HAVE STOPPED ASKING. HE HAS STOPPED PRETENDING.",
            "THE MARKED ROOM IS MARKED FOR A REASON. THE REASON IS THE PEOPLE WAITING OUTSIDE IT.",
            "THE CAT IN THE HIDEOUT IS NOT ON ANY ROSTER. IT IS FED BETTER THAN ANYONE ON THE ROSTER.",
            "THE FIGURE SEEN IN THE LAB ARENA IS 'A TRICK OF THE LIGHT'. THE LIGHT HAS NOT BEEN ON IN THREE YEARS.",
            "SOMEONE KEEPS CHAMBERING A ROUND IN EVERY RIFLE IN THE RACK. IT IS SAFER THAN THE ALTERNATIVE. PLEASE STOP ANYWAY.",
            "JAEGER DENIES SUPPLYING THE MOONSHINE. JAEGER'S CAMP SMELLS OF MOONSHINE. DRAW YOUR OWN CONCLUSIONS.",
            "THE SECURE CONTAINER IS FOR MISSION ITEMS. WE KNOW WHAT YOU KEEP IN IT. EVERYONE KEEPS THE SAME THING IN IT.",
            "IF YOU CAN READ THIS, THE FILTER HAS FAILED AGAIN. FORGET THE WORD 'TERMINUS' AND CARRY ON.",
            "SOMEONE HAS BEEN LEAVING TUSHONKA ON THE COMMANDER'S DESK. IT IS NOT FUNNY. IT IS, A LITTLE.",
            "THE OPERATOR WHO KEEPS RENAMING THE SHARED MAP FOLDER TO 'EXTRACT_HERE' HAS BEEN IDENTIFIED. HELLO.",
            "DO NOT ANSWER THE FIELD TELEPHONE IN THE EMPTY BUNKER ON RESERVE. NOTHING IS CONNECTED TO IT.",
            "THE HIDEOUT CAT HAS BEEN SEEN IN THE BLUE LAB. NOBODY KNOWS HOW IT GOT IN. NOBODY IS GOING IN TO GET IT.",
            "THE 'LOOT GOBLIN' AWARD IS NOT AN OFFICIAL DECORATION. PLEASE STOP WEARING IT TO FORMATION.",
            "WHOEVER LEFT A BACKPACK FULL OF BOLTS IN THE CORRIDOR: MECHANIC WANTS THEM. MECHANIC ALWAYS WANTS THEM.",
            "THE LIGHTHOUSE KEEPER SENT A CARD. IT ARRIVED IN JUNE. IT WAS UNSIGNED. IT KNEW WHERE THE HIDEOUT IS.",
            "SOMEONE IS DRAWING SMILEY FACES ON THE LAB'S OBSERVATION WINDOWS. FROM THE INSIDE.",
            "AN OPERATOR CLAIMS TO HAVE BEEN SHOT BY A SCAV THROUGH TWO WALLS. THE WALLS HAVE BEEN SENT FOR ANALYSIS.",
            "THE MARKED KEY IS NOT A STATUS SYMBOL. STOP WEARING IT ON YOUR DOG TAGS.",
            "THE NETWORK PASSWORD WAS 'KAPPA'. IT HAS BEEN CHANGED. IT IS NOW 'KAPPA2'.",
            "THE BITCOIN FARM IS NOT A UNIT ASSET. IT IS ALSO THE ONLY THING IN THE HIDEOUT THAT PAYS FOR ITSELF.",
            "IF FOUND, RETURN THIS TERMINAL TO: [THE REST OF THIS MESSAGE HAS BEEN REMOVED]",
            "WE KNOW ABOUT THE WIGGLE. BOTH SIDES KNOW ABOUT THE WIGGLE.",
            "A NOTE IN A RECOVERED SECURE CONTAINER READS 'GRENADE FOR LATER'. WE WOULD LIKE TO KNOW WHEN LATER IS.",
            "THERE IS NO ARMOURED TRAIN. STOP ASKING ABOUT THE ARMOURED TRAIN. STOP LISTENING FOR THE ARMOURED TRAIN.",
            "WHOEVER KEEPS HIDING IN THE BUSHES BY THE HIDEOUT DOOR: WE CAN SEE YOU. SO CAN THE ENEMY.",
            "THE PHRASE 'IT'S A PVE SERVER' HAS BEEN OVERHEARD ON THE NET. NOBODY KNOWS WHAT IT MEANS. EVERYONE IS UNEASY.",
            "THE HIDEOUT CLOCK RUNS SEVEN TIMES FAST. IT IS SET TO TARKOV TIME. IT IS THE ONLY ACCURATE CLOCK WE HAVE.",
            "THE COLLECTOR HAS WRITTEN TO ASK FOR THE HIDEOUT CAT. THE ANSWER IS NO. THE ANSWER WILL ALWAYS BE NO.",
            "SOMEONE SOLD THE HIDEOUT'S SALT ON THE FLEA MARKET. THE PRICE WAS, FRANKLY, IMPRESSIVE.",
            "THE VOICES IN THE VENTILATION ARE THE AIR FILTER. THE AIR FILTER HAS BEEN SWITCHED OFF FOR A WEEK.",
            "THE OTHER SIDE'S CAT HAS BEEN SEEN WEARING OUR SIDE'S ARMBAND. A CEASEFIRE IS BEING DISCUSSED. FOR THE CATS."
        };

        private static readonly string[] Senders =
        {
            "REGIONAL INTELLIGENCE", "SIGNALS DESK", "FIELD OBSERVATION", "OVERWATCH", "THREAT ASSESSMENT",
            "SITUATION ROOM", "INTERCEPT ANALYSIS", "MARKET WATCH", "PERIMETER OBSERVATION", "DRONE OPERATIONS"
        };

        /// <summary>A line of regional intel, filled in.</summary>
        internal static string IntelLine(System.Random random) => Fill(IntelTemplate(random), random);

        /// <summary>A real place in the region, for labelling a camera, a destination, a dateline.</summary>
        internal static void Location(System.Random random, out string map, out string place)
        {
            Spot spot = Maps[random.Next(Maps.Length)];
            map = spot.Map;
            place = spot.Places[random.Next(spot.Places.Length)];
        }

        /// <summary>An intel line still holding its placeholders, for filling in later; one the player has not seen lately.</summary>
        internal static string IntelTemplate(System.Random random) => Archive.PickLine("intel", Intel, random);

        /// <summary>A note nobody admits to sending; one the player has not seen lately.</summary>
        internal static string EasterEgg(System.Random random) => Archive.PickLine("note", EasterEggs, random);

        internal static string IntelSender(System.Random random) => Senders[random.Next(Senders.Length)];

        /// <summary>
        /// Fills the placeholders: {boss}{bossmap}{bossplace} are one consistent sighting,
        /// {map}{place} one consistent location; {faction} is the player's side and {enemy} the
        /// other; {trader} {item} {nick} {n2} {n3} {n4} {pct} speak for themselves.
        /// </summary>
        internal static string Fill(string template, System.Random random)
        {
            if (template.IndexOf('{') < 0)
            {
                return template;
            }
            string[] boss = Bosses[random.Next(Bosses.Length)];
            Spot spot = Maps[random.Next(Maps.Length)];
            string nick = StartupTracker.Nickname;
            string side = StartupTracker.Side ?? StartupTracker.LastSide;
            string own = string.IsNullOrEmpty(side) ? (random.Next(2) == 0 ? "USEC" : "BEAR") : side;
            string enemy = own == "BEAR" ? "USEC" : "BEAR";
            return template
                .Replace("{bossmap}", boss[1])
                .Replace("{bossplace}", boss[2])
                .Replace("{boss}", boss[0])
                .Replace("{map}", spot.Map)
                .Replace("{place}", spot.Places[random.Next(spot.Places.Length)])
                .Replace("{trader}", Traders[random.Next(Traders.Length)])
                .Replace("{item}", Items[random.Next(Items.Length)])
                .Replace("{faction}", own)
                .Replace("{enemy}", enemy)
                .Replace("{nick}", string.IsNullOrEmpty(nick) ? "[REDACTED]" : nick.ToUpperInvariant())
                .Replace("{n2}", random.Next(2, 60).ToString())
                .Replace("{n3}", random.Next(40, 400).ToString())
                .Replace("{n4}", random.Next(3, 6).ToString())
                .Replace("{pct}", random.Next(8, 140).ToString());
        }

        /// <summary>A few intel headlines for the ticker.</summary>
        internal static IEnumerable<string> Headlines(System.Random random, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return IntelLine(random);
            }
        }
    }
}
