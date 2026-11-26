using System.Globalization;
using System.Text.RegularExpressions;

namespace ClockApp.Reminders
{
    public enum ReminderKind { Morning, Lunch, Evening, Night, ClockChange }

    public sealed record Reminder(ReminderKind Kind, string Title, string Body);

    /// <summary>
    /// Rédige les rappels. Pour chaque moment de la journée, un titre et une phrase sont tirés
    /// séparément dans deux grandes listes (certaines lignes réservées à un jour précis ou au week-end),
    /// ce qui donne des centaines de combinaisons. Les lignes utilisées récemment sont mises de côté
    /// pour ne pas revoir la même chose d'un jour à l'autre.
    /// </summary>
    /// <remarks>
    /// Jetons disponibles :
    /// {ville} « Tokyo », « Paris et Berlin » · {à_ville} « à Tokyo », « au Caire » · {À_ville} idem en début de phrase ·
    /// {heure} heure là-bas · {local} heure chez l'utilisateur · {jour} / {Jour} jour de la semaine là-bas ·
    /// [singulier|pluriel] accord selon le nombre de villes.
    /// Les titres ne contiennent pas {heure} : c'est la phrase qui la donne, pour éviter les répétitions.
    /// </remarks>
    public sealed partial class ReminderMessages
    {
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        public static TimeOnly TimeOf(ReminderKind kind) => kind switch
        {
            ReminderKind.Morning => new(9, 0),
            ReminderKind.Lunch => new(12, 0),
            ReminderKind.Evening => new(18, 0),
            ReminderKind.Night => new(22, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        private sealed record Line(ReminderKind Kind, string Text, Func<DayOfWeek, bool> When);

        private const ReminderKind Morning = ReminderKind.Morning;
        private const ReminderKind Lunch = ReminderKind.Lunch;
        private const ReminderKind Evening = ReminderKind.Evening;
        private const ReminderKind Night = ReminderKind.Night;

        private static bool Any(DayOfWeek d) => true;
        private static bool Weekday(DayOfWeek d) => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        private static bool Weekend(DayOfWeek d) => !Weekday(d);
        /// <summary>Soirs suivis d'un jour travaillé (dimanche → jeudi).</summary>
        private static bool SchoolNight(DayOfWeek d) => d is not (DayOfWeek.Friday or DayOfWeek.Saturday);
        private static bool PartyNight(DayOfWeek d) => !SchoolNight(d);
        private static Func<DayOfWeek, bool> On(DayOfWeek day) => d => d == day;

        private static readonly DayOfWeek Mon = DayOfWeek.Monday, Tue = DayOfWeek.Tuesday, Wed = DayOfWeek.Wednesday,
            Thu = DayOfWeek.Thursday, Fri = DayOfWeek.Friday, Sat = DayOfWeek.Saturday, Sun = DayOfWeek.Sunday;

        // ─────────────────────────────── Titres ───────────────────────────────
        private static readonly Line[] Titles =
        {
            // Matin — semaine
            new(Morning, "La journée commence {à_ville}", Weekday),
            new(Morning, "Bonjour, {ville} !", Any),
            new(Morning, "{ville} [se réveille|se réveillent]", Any),
            new(Morning, "Réveil {à_ville}", Weekday),
            new(Morning, "C’est le matin {à_ville}", Any),
            new(Morning, "Début de matinée {à_ville}", Any),
            new(Morning, "{ville} [se met|se mettent] en route", Weekday),
            new(Morning, "Nouvelle journée {à_ville}", Any),
            new(Morning, "Debout, {ville} !", Weekday),
            new(Morning, "{Jour} matin {à_ville}", Any),
            new(Morning, "Premiers pas de la journée {à_ville}", Weekday),
            new(Morning, "{ville} [ouvre ses|ouvrent leurs] portes", Weekday),
            new(Morning, "La ville s’active {à_ville}", Weekday),
            new(Morning, "Heure de pointe {à_ville}", Weekday),
            new(Morning, "Lundi matin {à_ville}", On(Mon)),
            new(Morning, "La semaine reprend {à_ville}", On(Mon)),
            new(Morning, "Retour au boulot {à_ville}", On(Mon)),
            new(Morning, "Milieu de semaine {à_ville}", On(Wed)),
            new(Morning, "Le week-end approche {à_ville}", On(Thu)),
            new(Morning, "Dernier matin de la semaine {à_ville}", On(Fri)),
            new(Morning, "Enfin vendredi {à_ville}", On(Fri)),
            // Matin — week-end
            new(Morning, "Matin tranquille {à_ville}", Weekend),
            new(Morning, "Week-end {à_ville}", Weekend),
            new(Morning, "Grasse matinée {à_ville} ?", Weekend),
            new(Morning, "Pas de réveil qui sonne {à_ville}", Weekend),
            new(Morning, "{Jour} au calme {à_ville}", Weekend),
            new(Morning, "Matinée paresseuse {à_ville}", Weekend),
            new(Morning, "{ville} [prend son|prennent leur] temps", Weekend),

            // Midi — semaine
            new(Lunch, "Pause déjeuner {à_ville}", Weekday),
            new(Lunch, "Midi {à_ville}", Any),
            new(Lunch, "On passe à table {à_ville}", Any),
            new(Lunch, "{ville} [fait|font] une pause", Weekday),
            new(Lunch, "C’est midi {à_ville}", Any),
            new(Lunch, "L’heure du déjeuner {à_ville}", Any),
            new(Lunch, "Pause de midi {à_ville}", Weekday),
            new(Lunch, "{ville} [déjeune|déjeunent]", Any),
            new(Lunch, "Mi-journée {à_ville}", Any),
            new(Lunch, "Bon appétit, {ville} !", Any),
            new(Lunch, "La ville marque une pause {à_ville}", Weekday),
            new(Lunch, "Dernier déjeuner de la semaine {à_ville}", On(Fri)),
            // Midi — week-end
            new(Lunch, "{Jour} midi {à_ville}", Weekend),
            new(Lunch, "Midi tranquille {à_ville}", Weekend),
            new(Lunch, "Déjeuner du week-end {à_ville}", Weekend),
            new(Lunch, "Repas du dimanche {à_ville}", On(Sun)),

            // Soir — semaine
            new(Evening, "Fin de journée {à_ville}", Weekday),
            new(Evening, "{ville} [termine sa|terminent leur] journée", Weekday),
            new(Evening, "Les bureaux ferment {à_ville}", Weekday),
            new(Evening, "C’est le soir {à_ville}", Any),
            new(Evening, "Sortie du travail {à_ville}", Weekday),
            new(Evening, "Début de soirée {à_ville}", Any),
            new(Evening, "{Jour} soir {à_ville}", Any),
            new(Evening, "La journée se termine {à_ville}", Any),
            new(Evening, "Rush du soir {à_ville}", Weekday),
            new(Evening, "{ville} [rentre|rentrent] à la maison", Weekday),
            new(Evening, "Bonsoir, {ville} !", Any),
            new(Evening, "Le soir tombe {à_ville}", Any),
            new(Evening, "Premier soir de la semaine {à_ville}", On(Mon)),
            new(Evening, "Le week-end commence {à_ville}", On(Fri)),
            new(Evening, "Enfin le week-end {à_ville}", On(Fri)),
            // Soir — week-end
            new(Evening, "Soirée de week-end {à_ville}", Weekend),
            new(Evening, "Samedi soir {à_ville}", On(Sat)),
            new(Evening, "Fin du week-end {à_ville}", On(Sun)),

            // Nuit
            new(Night, "Il se fait tard {à_ville}", Any),
            new(Night, "Bonne nuit, {ville}", Any),
            new(Night, "Soirée bien avancée {à_ville}", Any),
            new(Night, "La nuit s’installe {à_ville}", Any),
            new(Night, "Fin de soirée {à_ville}", Any),
            new(Night, "{ville} [s’endort|s’endorment]", SchoolNight),
            new(Night, "Tard le soir {à_ville}", Any),
            new(Night, "{Jour} soir {à_ville}", Any),
            new(Night, "Extinction des feux {à_ville}", SchoolNight),
            new(Night, "Calme du soir {à_ville}", SchoolNight),
            new(Night, "Nuit de fête {à_ville} ?", PartyNight),
            new(Night, "La nuit ne fait que commencer {à_ville}", PartyNight),
            new(Night, "Dernière soirée du week-end {à_ville}", On(Sun)),
        };

        // ─────────────────────────────── Phrases ───────────────────────────────
        private static readonly Line[] Bodies =
        {
            // Matin — semaine
            new(Morning, "Il est {heure} là-bas : la ville se réveille et les rues se remplissent.", Weekday),
            new(Morning, "{Jour} matin, {heure} là-bas. Les cafés sont pleins et les bureaux ouvrent.", Weekday),
            new(Morning, "Il est {heure} : c’est l’heure de pointe dans les transports là-bas.", Weekday),
            new(Morning, "La matinée est lancée là-bas, pendant qu’il est {local} chez vous.", Weekday),
            new(Morning, "Il est {heure}. Les boutiques lèvent le rideau et la ville prend son rythme.", Weekday),
            new(Morning, "{Jour}, {heure} là-bas : une nouvelle journée commence.", Any),
            new(Morning, "Il est {heure} là-bas. Les écoles ont ouvert leurs portes et les rues sont animées.", Weekday),
            new(Morning, "{heure} là-bas : les bus et les métros tournent à plein régime.", Weekday),
            new(Morning, "Il est {heure}. Ça sent le café dans toutes les rues là-bas.", Any),
            new(Morning, "Les premières réunions de la journée commencent là-bas. Chez vous, il est {local}.", Weekday),
            new(Morning, "Il est {heure} là-bas : les marchés s’installent et les livraisons s’enchaînent.", Any),
            new(Morning, "Toute la ville est debout là-bas : il est {heure}.", Weekday),
            new(Morning, "{heure} là-bas : les embouteillages du matin battent leur plein.", Weekday),
            new(Morning, "Il est {heure} là-bas, et {local} chez vous. Là-bas, la journée ne fait que commencer.", Any),
            new(Morning, "Il est {heure} là-bas : les ordinateurs s’allument dans les bureaux.", Weekday),
            new(Morning, "La ville a repris vie : il est {heure} là-bas.", Weekday),
            new(Morning, "Il est {heure} là-bas. Une longue journée commence.", Weekday),
            new(Morning, "{Jour} matin là-bas : les trottoirs sont pleins de gens pressés.", Weekday),
            new(Morning, "Il est {heure}. Les chantiers redémarrent et les rideaux métalliques remontent là-bas.", Weekday),
            new(Morning, "Les rues s’animent là-bas : il est {heure}, chacun file vers sa journée.", Weekday),
            new(Morning, "Il est {heure} là-bas. Les écoliers sont en classe depuis peu.", Weekday),
            new(Morning, "Il est {heure} : le premier café de la journée est déjà bu pour beaucoup là-bas.", Any),
            new(Morning, "Lundi, {heure} là-bas : retour au travail et à l’école après le week-end.", On(Mon)),
            new(Morning, "Nouvelle semaine là-bas : il est {heure}, tout le monde reprend le rythme.", On(Mon)),
            new(Morning, "Le week-end est fini là-bas : il est {heure} et la semaine redémarre.", On(Mon)),
            new(Morning, "Mardi, {heure} là-bas : la semaine est bien lancée.", On(Tue)),
            new(Morning, "Mercredi, {heure} là-bas : la semaine est à moitié faite.", On(Wed)),
            new(Morning, "Jeudi, {heure} là-bas. Le week-end se rapproche.", On(Thu)),
            new(Morning, "Vendredi, {heure} là-bas. Plus qu’une journée avant le week-end.", On(Fri)),
            new(Morning, "Dernière ligne droite là-bas : c’est vendredi et il est {heure}.", On(Fri)),
            // Matin — week-end
            new(Morning, "Il est {heure}, mais c’est le week-end là-bas : la ville se réveille en douceur.", Weekend),
            new(Morning, "{Jour}, {heure} là-bas. Les rues sont encore calmes.", Weekend),
            new(Morning, "Il est {heure}. Marchés, brunchs et balades : la ville prend son temps aujourd’hui.", Weekend),
            new(Morning, "{heure} là-bas : pas de bouchons ce matin, c’est le week-end.", Weekend),
            new(Morning, "Il est {heure}. Les parcs commencent à se remplir de joggeurs et de promeneurs là-bas.", Weekend),
            new(Morning, "Les bureaux sont fermés là-bas : il est {heure} et la ville respire.", Weekend),
            new(Morning, "{Jour} matin, {heure} là-bas. Beaucoup dorment encore.", Weekend),
            new(Morning, "Il est {heure} là-bas : les cafés servent les premiers petits-déjeuners du week-end.", Weekend),
            new(Morning, "Il est {heure} là-bas, et personne n’est pressé aujourd’hui.", Weekend),
            new(Morning, "Samedi, {heure} là-bas : place aux courses et aux sorties.", On(Sat)),
            new(Morning, "Dimanche, {heure} là-bas : c’est le matin le plus calme de la semaine.", On(Sun)),

            // Midi — semaine
            new(Lunch, "Il est midi là-bas : les restaurants se remplissent et les bureaux se vident pour une heure.", Weekday),
            new(Lunch, "C’est l’heure du déjeuner là-bas, et il est {local} chez vous.", Any),
            new(Lunch, "Midi là-bas : la ville fait une pause avant l’après-midi.", Weekday),
            new(Lunch, "Il est midi là-bas : les terrasses et les cantines sont prises d’assaut.", Weekday),
            new(Lunch, "La moitié de la journée est déjà passée là-bas.", Weekday),
            new(Lunch, "Il est midi : files d’attente devant les food-trucks et les sandwicheries là-bas.", Weekday),
            new(Lunch, "Midi pile là-bas. Beaucoup mangent sur le pouce, entre deux rendez-vous.", Weekday),
            new(Lunch, "C’est le coup de feu dans les cuisines là-bas : il est midi.", Any),
            new(Lunch, "Il est midi là-bas : les écoliers sont à la cantine et les bureaux tournent au ralenti.", Weekday),
            new(Lunch, "Pause bien méritée là-bas : il est midi, et {local} chez vous.", Weekday),
            new(Lunch, "Il est midi là-bas. L’après-midi reprendra dans une petite heure.", Weekday),
            new(Lunch, "Les rues se remplissent à nouveau là-bas : c’est la sortie de midi.", Weekday),
            new(Lunch, "Il est midi là-bas : les livreurs à vélo n’arrêtent pas.", Any),
            new(Lunch, "Midi là-bas : les bancs des parcs se remplissent de gens qui déjeunent dehors.", Weekday),
            new(Lunch, "Il est midi là-bas. Le soleil est au plus haut, la ville souffle un peu.", Any),
            new(Lunch, "Vendredi midi là-bas : le dernier déjeuner de la semaine de travail.", On(Fri)),
            new(Lunch, "Mercredi midi là-bas : la semaine bascule dans sa deuxième moitié.", On(Wed)),
            // Midi — week-end
            new(Lunch, "Il est midi là-bas : déjeuners en famille et tables entre amis.", Weekend),
            new(Lunch, "Midi là-bas, et c’est le week-end : personne ne regarde sa montre.", Weekend),
            new(Lunch, "Il est midi : les brunchs se terminent et les terrasses sont pleines là-bas.", Weekend),
            new(Lunch, "{Jour} midi là-bas : la ville vit au ralenti.", Weekend),
            new(Lunch, "Il est midi là-bas. Les marchés remballent, les restaurants affichent complet.", Weekend),
            new(Lunch, "Dimanche midi là-bas : c’est souvent le repas le plus long de la semaine.", On(Sun)),
            new(Lunch, "Samedi midi là-bas : entre deux courses, on s’arrête pour manger.", On(Sat)),

            // Soir — semaine
            new(Evening, "Il est {heure} là-bas : les bureaux se vident et les transports sont bondés.", Weekday),
            new(Evening, "Il est {heure} là-bas : place à la soirée. Chez vous, il est {local}.", Any),
            new(Evening, "La journée de travail se termine là-bas.", Weekday),
            new(Evening, "Retour à la maison pour beaucoup : c’est l’heure de pointe du soir là-bas.", Weekday),
            new(Evening, "Il est {heure}. Les magasins voient arriver la foule d’après le travail.", Weekday),
            new(Evening, "{heure} là-bas : les salles de sport et les supermarchés se remplissent.", Weekday),
            new(Evening, "Il est {heure} là-bas : on rentre, on cuisine, on souffle un peu.", Any),
            new(Evening, "Les lumières des bureaux s’éteignent une à une là-bas.", Weekday),
            new(Evening, "Il est {heure} là-bas : les rues s’animent à nouveau, mais dans l’autre sens.", Weekday),
            new(Evening, "Fin de journée là-bas : il est {heure}, et {local} chez vous.", Any),
            new(Evening, "Il est {heure}. Les terrasses accueillent les premiers clients de la soirée là-bas.", Any),
            new(Evening, "{Jour} soir là-bas : la ville passe en mode détente.", Any),
            new(Evening, "Il est {heure} là-bas : les enfants rentrent de leurs activités, les cuisines s’allument.", Weekday),
            new(Evening, "Il est {heure}. Les restaurants dressent les tables pour le dîner là-bas.", Any),
            new(Evening, "Bouchons du soir là-bas : il est {heure}, tout le monde rentre en même temps.", Weekday),
            new(Evening, "Il est {heure} là-bas : la ville change de rythme pour la soirée.", Any),
            new(Evening, "Premier soir de la semaine là-bas : il est {heure}.", On(Mon)),
            new(Evening, "Jeudi soir là-bas : plus qu’une journée avant le week-end.", On(Thu)),
            new(Evening, "Vendredi, {heure} là-bas : la semaine est finie, place au week-end.", On(Fri)),
            new(Evening, "Il est {heure} : les terrasses se remplissent là-bas, le week-end commence.", On(Fri)),
            new(Evening, "C’est vendredi soir là-bas : les bureaux sont déjà vides.", On(Fri)),
            // Soir — week-end
            new(Evening, "Il est {heure} là-bas : la soirée du week-end commence.", Weekend),
            new(Evening, "{Jour}, {heure} là-bas : restaurants, cinémas et sorties.", Weekend),
            new(Evening, "Il est {heure}. Le week-end continue tranquillement là-bas.", Weekend),
            new(Evening, "Samedi soir là-bas : la ville se prépare à sortir.", On(Sat)),
            new(Evening, "Dimanche soir là-bas : on prépare déjà la semaine qui arrive.", On(Sun)),
            new(Evening, "Il est {heure} : dernière soirée du week-end là-bas.", On(Sun)),

            // Nuit
            new(Night, "Il est {heure} là-bas : la ville commence à s’endormir.", SchoolNight),
            new(Night, "{heure} là-bas : la plupart des gens vont bientôt se coucher.", SchoolNight),
            new(Night, "La soirée touche à sa fin là-bas. Chez vous, il est {local}.", Any),
            new(Night, "Il est {heure} : les rues se calment et les lumières s’éteignent peu à peu.", Any),
            new(Night, "Les transports se vident là-bas : il est {heure}.", Any),
            new(Night, "{heure} là-bas : c’est l’heure du dernier épisode avant d’aller dormir.", SchoolNight),
            new(Night, "Il est {heure}. Les restaurants servent leurs dernières tables là-bas.", Any),
            new(Night, "Le calme revient là-bas : il est {heure}.", SchoolNight),
            new(Night, "Il est {heure} là-bas : les enfants dorment depuis longtemps.", Any),
            new(Night, "Là-bas, la journée est presque finie. Chez vous, il est {local}.", Any),
            new(Night, "Il est {heure}. Seuls les noctambules et les travailleurs de nuit sont encore dehors là-bas.", SchoolNight),
            new(Night, "{Jour} soir, {heure} là-bas : la ville baisse le volume.", SchoolNight),
            new(Night, "Il est {heure} là-bas : demain, on se lève tôt.", SchoolNight),
            new(Night, "Il est {heure}. Les rideaux se ferment et les écrans s’éteignent là-bas.", SchoolNight),
            new(Night, "{heure} là-bas : les rues ne sont plus éclairées que par les lampadaires.", Any),
            new(Night, "Il est {heure} là-bas : les boulangers seront les premiers debout demain.", SchoolNight),
            new(Night, "Il est {heure}, et c’est {jour} : pour les noctambules, la soirée ne fait que commencer.", PartyNight),
            new(Night, "{Jour} soir là-bas : bars et salles de concert sont pleins.", PartyNight),
            new(Night, "Il est {heure} là-bas : la soirée bat son plein, personne ne se lève tôt demain.", PartyNight),
            new(Night, "Il est {heure} là-bas : les files d’attente s’allongent devant les clubs.", On(Sat)),
            new(Night, "Il est {heure} là-bas : dernier soir du week-end, réveil demain matin.", On(Sun)),
        };

        private readonly Random _random = new();
        private readonly Dictionary<(ReminderKind, bool IsTitle), Queue<Line>> _recent = new();

        public bool HasTemplate(ReminderKind kind, DayOfWeek day) =>
            Titles.Any(t => t.Kind == kind && t.When(day)) && Bodies.Any(b => b.Kind == kind && b.When(day));

        /// <param name="cities">Villes concernées (même heure locale).</param>
        /// <param name="cityTime">Heure actuelle dans ces villes.</param>
        /// <param name="localTime">Heure actuelle chez l'utilisateur.</param>
        /// <returns>Le rappel, ou null si aucune phrase ne correspond à ce jour-là.</returns>
        public Reminder? Compose(ReminderKind kind, IReadOnlyList<string> cities, DateTime cityTime, DateTime localTime)
        {
            if (cities.Count == 0) return null;
            var title = Pick(Titles, kind, isTitle: true, cityTime.DayOfWeek);
            var body = Pick(Bodies, kind, isTitle: false, cityTime.DayOfWeek);
            if (title is null || body is null) return null;

            var values = new Dictionary<string, string>
            {
                ["ville"] = JoinCities(cities),
                ["à_ville"] = AtCities(cities),
                ["À_ville"] = Capitalize(AtCities(cities)),
                ["heure"] = FormatHour(cityTime),
                ["local"] = FormatHour(localTime),
                ["jour"] = cityTime.ToString("dddd", Fr),
                ["Jour"] = Capitalize(cityTime.ToString("dddd", Fr)),
            };

            return new Reminder(kind, Fill(title.Text, values, cities.Count), Fill(body.Text, values, cities.Count));
        }

        /// <summary>
        /// Tire une ligne au hasard parmi celles valables ce jour-là, en écartant les deux tiers
        /// les plus récemment utilisés : on ne revoit pas la même ligne avant un bon moment.
        /// </summary>
        private Line? Pick(Line[] lines, ReminderKind kind, bool isTitle, DayOfWeek day)
        {
            var pool = lines.Where(l => l.Kind == kind && l.When(day)).ToList();
            if (pool.Count == 0) return null;

            if (!_recent.TryGetValue((kind, isTitle), out var recent))
                _recent[(kind, isTitle)] = recent = new Queue<Line>();

            var fresh = pool.Where(l => !recent.Contains(l)).ToList();
            var candidates = fresh.Count > 0 ? fresh : pool;
            var line = candidates[_random.Next(candidates.Count)];

            recent.Enqueue(line);
            while (recent.Count > Math.Max(1, pool.Count * 2 / 3)) recent.Dequeue();
            return line;
        }

        private static string Fill(string text, Dictionary<string, string> values, int count)
        {
            text = Agreement().Replace(text, m => count > 1 ? m.Groups[2].Value : m.Groups[1].Value);
            text = Token().Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
            return Capitalize(text);
        }

        /// <summary>« 9 h », « 14 h 05 » (espaces insécables pour ne pas couper l'heure en fin de ligne).</summary>
        private static string FormatHour(DateTime t) =>
            t.Minute == 0 ? $"{t.Hour} h" : $"{t.Hour} h {t.Minute:00}";

        internal static string JoinCities(IReadOnlyList<string> names) => names.Count switch
        {
            1 => names[0],
            <= 3 => string.Join(", ", names.Take(names.Count - 1)) + " et " + names[^1],
            _ => $"{names[0]}, {names[1]} et {names.Count - 2} autres villes",
        };

        /// <summary>« à Tokyo », « au Caire », « à Paris, à Berlin et au Caire ».</summary>
        private static string AtCities(IReadOnlyList<string> names)
        {
            if (names.Count > 3) return "à " + JoinCities(names);
            if (!names.Any(HasArticle)) return "à " + JoinCities(names);
            return JoinCities(names.Select(At).ToList());
        }

        private static bool HasArticle(string city) =>
            city.StartsWith("Le ", StringComparison.Ordinal) || city.StartsWith("Les ", StringComparison.Ordinal);

        private static string At(string city) =>
            city.StartsWith("Le ", StringComparison.Ordinal) ? "au " + city[3..]
            : city.StartsWith("Les ", StringComparison.Ordinal) ? "aux " + city[4..]
            : "à " + city;

        internal static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Fr) + s[1..];

        [GeneratedRegex(@"\[([^|\]]*)\|([^\]]*)\]")]
        private static partial Regex Agreement();

        [GeneratedRegex(@"\{([^}]+)\}")]
        private static partial Regex Token();
    }
}
