using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Configuration;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Domain.Localization;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Domain.Staff;
using Athkar.Areas.Domain.Support;
using Athkar.Areas.Domain.Widgets;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Security;
using Athkar.Shareds.Text;

namespace Athkar.DataAccess.Seeders;

/// <summary>
/// Brings a fresh database up to something a person can sign into and an app can
/// talk to: one super-admin, two languages, the settings row, the chapters, and
/// a handful of sound adhkar in each so no screen opens empty.
///
/// Every step is guarded, so this runs on every startup and does nothing on the
/// second. Content is seeded <b>only when there is none at all</b> — an editor's
/// deletion is a decision, and a seeder that restores what somebody removed is a
/// bug that takes a week to notice.
/// </summary>
public static class DataSeeder
{
    public const string DefaultAdminEmail = "admin@athkari.app";

    /// <summary>
    /// The development password. A deployment is expected to change it at once,
    /// which is why the seeder logs a warning naming it rather than pretending
    /// this is a secret.
    /// </summary>
    public const string DefaultAdminPassword = "Athkari!2026";

    public static async Task SeedAsync(DatabaseService db, ILogger? logger = null)
    {
        await SeedLanguages(db);
        await SeedConfiguration(db);
        await SeedWidgetSettings(db);
        await WidgetCatalogSeeder.SeedAsync(db);
        await SeedAdmin(db, logger);
        await SeedContent(db);
        await QuranicAthkarSeeder.SeedAsync(db, logger);
        await SeedReminders(db);
        await SeedFaq(db);
    }

    private static async Task SeedLanguages(DatabaseService db)
    {
        if (await db.Languages.AnyAsync()) return;

        db.Languages.AddRange(
            new AppLanguage
            {
                Code = "ar",
                NativeName = "العربية",
                EnglishName = "Arabic",
                IsRtl = true,
                IsEnabled = true,
                IsDefault = true,
                SortOrder = 0,
            },
            new AppLanguage
            {
                Code = "en",
                NativeName = "English",
                EnglishName = "English",
                IsRtl = false,
                IsEnabled = true,
                IsDefault = false,
                SortOrder = 1,
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedConfiguration(DatabaseService db)
    {
        if (await db.AppConfigurations.AnyAsync()) return;

        db.AppConfigurations.Add(new AppConfiguration());
        await db.SaveChangesAsync();
    }

    private static async Task SeedWidgetSettings(DatabaseService db)
    {
        if (await db.WidgetSettings.AnyAsync()) return;

        db.WidgetSettings.Add(new WidgetSettings());
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdmin(DatabaseService db, ILogger? logger)
    {
        if (await db.Users.AnyAsync()) return;

        var user = new User
        {
            Email = DefaultAdminEmail,
            FullName = "Administrator",
            PasswordHash = new PasswordHasher().Hash(DefaultAdminPassword),
            LanguageCode = "ar",
            IsActive = true,
        };
        user.Roles.Add(new UserRole { Role = Roles.SuperAdmin });

        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger?.LogWarning(
            "Seeded the first administrator as {Email} with the default password. Change it now.",
            DefaultAdminEmail);
    }

    /// <summary>
    /// The chapters, and a few adhkar in each.
    ///
    /// Every seeded dhikr carries its book and number, because the rule that
    /// nothing publishes without a source applies to the seeder too — content
    /// that arrived through a back door is exactly the content nobody checks.
    /// </summary>
    private static async Task SeedContent(DatabaseService db)
    {
        if (await db.Categories.AnyAsync()) return;

        var chapters = new (string Key, string Ar, string En, string Icon, CategoryRhythm Rhythm, PrayerAnchor Anchor)[]
        {
            ("morning", "أذكار الصباح", "Morning remembrance", "sunrise", CategoryRhythm.Daily, PrayerAnchor.Sunrise),
            ("evening", "أذكار المساء", "Evening remembrance", "sunset", CategoryRhythm.Daily, PrayerAnchor.Asr),
            ("sleep", "أذكار النوم", "Before sleep", "moon", CategoryRhythm.Daily, PrayerAnchor.Bedtime),
            ("waking", "أذكار الاستيقاظ", "On waking", "sun", CategoryRhythm.Daily, PrayerAnchor.Fajr),
            ("after-prayer", "أذكار بعد الصلاة", "After the prayer", "prayer", CategoryRhythm.None, PrayerAnchor.None),
            ("food", "أذكار الطعام", "Food and drink", "food", CategoryRhythm.None, PrayerAnchor.None),
            ("clothing", "أذكار اللباس", "Dressing", "clothing", CategoryRhythm.None, PrayerAnchor.None),
            ("khala", "أذكار الخلاء", "Entering and leaving", "door", CategoryRhythm.None, PrayerAnchor.None),
            ("mosque", "أذكار المسجد", "The mosque", "mosque", CategoryRhythm.None, PrayerAnchor.None),
            ("travel", "أذكار السفر", "Travel", "travel", CategoryRhythm.None, PrayerAnchor.None),
            ("distress", "أذكار الكرب والهم", "Distress and anxiety", "heart", CategoryRhythm.None, PrayerAnchor.None),
            ("istighfar", "الاستغفار", "Seeking forgiveness", "drop", CategoryRhythm.None, PrayerAnchor.None),
            ("salat-nabi", "الصلاة على النبي ﷺ", "Blessings on the Prophet", "star", CategoryRhythm.None, PrayerAnchor.None),
            ("misc", "أذكار متفرقة", "Miscellaneous", "book", CategoryRhythm.None, PrayerAnchor.None),
        };

        var byKey = new Dictionary<string, AthkarCategory>();

        for (var i = 0; i < chapters.Length; i++)
        {
            var (key, ar, en, icon, rhythm, anchor) = chapters[i];

            var category = new AthkarCategory
            {
                Key = key,
                Icon = icon,
                SortOrder = i,
                Rhythm = rhythm,
                Anchor = anchor,
                IsPublished = true,
            };
            category.Translations.Add(new CategoryTranslation { LanguageCode = "ar", Name = ar });
            category.Translations.Add(new CategoryTranslation { LanguageCode = "en", Name = en });

            byKey[key] = category;
            db.Categories.Add(category);
        }

        await db.SaveChangesAsync();

        var seeds = new (string Category, string Arabic, string English, int Repeat,
            string Book, string Reference, HadithGrade Grade, string? GradedBy)[]
        {
            // ── The morning and evening sheet ──
            //
            // The set below is the one-page «أذكار الصباح والمساء» sheet, kept in
            // its printed order so the app reads top to bottom the way the paper
            // does. Where the sheet's own footnote and the primary collection
            // disagree on the locator, the primary collection wins and the
            // footnote's grading is carried in GradedBy — a grade without a
            // grader is an assertion, and the seeder does not get to make one.

            ("morning",
                "أَصْبَحْنَا وَأَصْبَحَ المُلْكُ لِلَّهِ، وَالحَمْدُ لِلَّهِ، لَا إِلَهَ إِلَّا اللهُ وَحْدَهُ لَا شَرِيكَ لَهُ، لَهُ المُلْكُ وَلَهُ الحَمْدُ وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ، رَبِّ أَسْأَلُكَ خَيْرَ مَا فِي هَذَا اليَوْمِ وَخَيْرَ مَا بَعْدَهُ، وَأَعُوذُ بِكَ مِنْ شَرِّ مَا فِي هَذَا اليَوْمِ وَشَرِّ مَا بَعْدَهُ، رَبِّ أَعُوذُ بِكَ مِنَ الكَسَلِ وَسُوءِ الكِبَرِ، رَبِّ أَعُوذُ بِكَ مِنْ عَذَابٍ فِي النَّارِ وَعَذَابٍ فِي القَبْرِ",
                "We have entered the morning and the dominion belongs to Allah; praise belongs to Allah. There is no god but Allah alone, without partner; His is the dominion and His the praise, and He is able to do all things. My Lord, I ask You for the good of this day and the good of what follows it, and I seek refuge in You from the evil of this day and the evil of what follows it. My Lord, I seek refuge in You from idleness and the wretchedness of old age. My Lord, I seek refuge in You from punishment in the Fire and punishment in the grave.",
                1, "صحيح مسلم", "٢٧٢٣", HadithGrade.Sahih, null),

            ("morning",
                "اللَّهُمَّ بِكَ أَصْبَحْنَا، وَبِكَ أَمْسَيْنَا، وَبِكَ نَحْيَا، وَبِكَ نَمُوتُ، وَإِلَيْكَ النُّشُورُ",
                "O Allah, by You we enter the morning and by You we enter the evening; by You we live and by You we die, and to You is the resurrection.",
                1, "السلسلة الصحيحة", "٢٦٢", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "اللَّهُمَّ أَنْتَ رَبِّي لَا إِلَهَ إِلَّا أَنْتَ، خَلَقْتَنِي وَأَنَا عَبْدُكَ، وَأَنَا عَلَى عَهْدِكَ وَوَعْدِكَ مَا اسْتَطَعْتُ، أَعُوذُ بِكَ مِنْ شَرِّ مَا صَنَعْتُ، أَبُوءُ لَكَ بِنِعْمَتِكَ عَلَيَّ، وَأَبُوءُ لَكَ بِذَنْبِي فَاغْفِرْ لِي، فَإِنَّهُ لَا يَغْفِرُ الذُّنُوبَ إِلَّا أَنْتَ",
                "O Allah, You are my Lord; there is no god but You. You created me and I am Your servant, and I hold to Your covenant and Your promise as much as I am able. I seek refuge in You from the evil of what I have done. I acknowledge Your favour upon me and I acknowledge my sin, so forgive me — for none forgives sins but You. (The chief manner of seeking forgiveness.)",
                1, "صحيح البخاري", "٦٣٠٦", HadithGrade.Sahih, null),

            ("morning",
                "اللَّهُمَّ عَالِمَ الغَيْبِ وَالشَّهَادَةِ، فَاطِرَ السَّمَاوَاتِ وَالأَرْضِ، رَبَّ كُلِّ شَيْءٍ وَمَلِيكَهُ، أَشْهَدُ أَنْ لَا إِلَهَ إِلَّا أَنْتَ، أَعُوذُ بِكَ مِنْ شَرِّ نَفْسِي، وَشَرِّ الشَّيْطَانِ وَشِرْكِهِ، وَأَنْ أَقْتَرِفَ عَلَى نَفْسِي سُوءًا أَوْ أَجُرَّهُ إِلَى مُسْلِمٍ",
                "O Allah, Knower of the unseen and the seen, Originator of the heavens and the earth, Lord and Sovereign of all things: I bear witness that there is no god but You. I seek refuge in You from the evil of my own self, and from the evil of Satan and his incitement to idolatry, and from bringing evil upon myself or drawing it upon a Muslim.",
                1, "صحيح الكلم الطيب", "٢٢", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "يَا حَيُّ يَا قَيُّومُ بِرَحْمَتِكَ أَسْتَغِيثُ، أَصْلِحْ لِي شَأْنِي كُلَّهُ، وَلَا تَكِلْنِي إِلَى نَفْسِي طَرْفَةَ عَيْنٍ",
                "O Living, O Sustaining One, by Your mercy I seek relief. Set right all my affairs, and do not entrust me to myself for the blink of an eye.",
                1, "صحيح الترغيب", "٦٦١", HadithGrade.Hasan, "الألباني"),

            ("morning",
                "اللَّهُمَّ إِنِّي أَسْأَلُكَ العَافِيَةَ فِي الدُّنْيَا وَالآخِرَةِ، اللَّهُمَّ إِنِّي أَسْأَلُكَ العَفْوَ وَالعَافِيَةَ فِي دِينِي وَدُنْيَايَ وَأَهْلِي وَمَالِي، اللَّهُمَّ اسْتُرْ عَوْرَاتِي وَآمِنْ رَوْعَاتِي، اللَّهُمَّ احْفَظْنِي مِنْ بَيْنِ يَدَيَّ وَمِنْ خَلْفِي، وَعَنْ يَمِينِي وَعَنْ شِمَالِي، وَمِنْ فَوْقِي، وَأَعُوذُ بِعَظَمَتِكَ أَنْ أُغْتَالَ مِنْ تَحْتِي",
                "O Allah, I ask You for well-being in this world and the next. O Allah, I ask You for pardon and well-being in my religion, my worldly life, my family and my wealth. O Allah, conceal my faults and calm my fears. O Allah, guard me from before me and behind me, from my right and my left, and from above me; and I seek refuge in Your greatness from being taken unawares from beneath me.",
                1, "صحيح الكلم الطيب", "٢٧", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "بِسْمِ اللهِ الَّذِي لَا يَضُرُّ مَعَ اسْمِهِ شَيْءٌ فِي الأَرْضِ وَلَا فِي السَّمَاءِ وَهُوَ السَّمِيعُ العَلِيمُ",
                "In the name of Allah, with whose name nothing on earth or in heaven can cause harm, and He is the All-Hearing, the All-Knowing.",
                3, "صحيح الترمذي", "٣٣٨٨", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "اللَّهُمَّ عَافِنِي فِي بَدَنِي، اللَّهُمَّ عَافِنِي فِي سَمْعِي، اللَّهُمَّ عَافِنِي فِي بَصَرِي، لَا إِلَهَ إِلَّا أَنْتَ. اللَّهُمَّ إِنِّي أَعُوذُ بِكَ مِنَ الكُفْرِ وَالفَقْرِ، اللَّهُمَّ إِنِّي أَعُوذُ بِكَ مِنْ عَذَابِ القَبْرِ، لَا إِلَهَ إِلَّا أَنْتَ",
                "O Allah, grant me well-being in my body. O Allah, grant me well-being in my hearing. O Allah, grant me well-being in my sight. There is no god but You. O Allah, I seek refuge in You from disbelief and poverty, and I seek refuge in You from the punishment of the grave. There is no god but You.",
                3, "صحيح أبي داود", "٥٠٩٠", HadithGrade.Hasan, "الألباني"),

            ("morning",
                "لَا إِلَهَ إِلَّا اللهُ وَحْدَهُ لَا شَرِيكَ لَهُ، لَهُ المُلْكُ وَلَهُ الحَمْدُ وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ",
                "There is no god but Allah alone, without partner; His is the dominion and His the praise, and He is able to do all things.",
                100, "صحيح أبي داود", "٥٠٧٧", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "لَا إِلَهَ إِلَّا اللهُ وَحْدَهُ لَا شَرِيكَ لَهُ، لَهُ المُلْكُ وَلَهُ الحَمْدُ، يُحْيِي وَيُمِيتُ، وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ",
                "There is no god but Allah alone, without partner; His is the dominion and His the praise. He gives life and causes death, and He is able to do all things.",
                10, "السلسلة الصحيحة", "٢٧٦٢", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "سُبْحَانَ اللهِ وَبِحَمْدِهِ",
                "Glory be to Allah, and praise be to Him.",
                100, "صحيح مسلم", "٢٦٩٢", HadithGrade.Sahih, null),

            ("morning",
                "اللهُ لَا إِلَهَ إِلَّا هُوَ الحَيُّ القَيُّومُ، لَا تَأْخُذُهُ سِنَةٌ وَلَا نَوْمٌ، لَهُ مَا فِي السَّمَاوَاتِ وَمَا فِي الأَرْضِ، مَنْ ذَا الَّذِي يَشْفَعُ عِنْدَهُ إِلَّا بِإِذْنِهِ، يَعْلَمُ مَا بَيْنَ أَيْدِيهِمْ وَمَا خَلْفَهُمْ، وَلَا يُحِيطُونَ بِشَيْءٍ مِنْ عِلْمِهِ إِلَّا بِمَا شَاءَ، وَسِعَ كُرْسِيُّهُ السَّمَاوَاتِ وَالأَرْضَ، وَلَا يَئُودُهُ حِفْظُهُمَا، وَهُوَ العَلِيُّ العَظِيمُ",
                "Ayat al-Kursi — Allah, there is no god but He, the Living, the Sustainer of all. Neither drowsiness nor sleep overtakes Him. To Him belongs whatever is in the heavens and whatever is on the earth…",
                1, "القرآن الكريم", "البقرة: ٢٥٥", HadithGrade.QuranVerse, null),

            ("morning",
                "قُلْ هُوَ اللهُ أَحَدٌ ۝ اللهُ الصَّمَدُ ۝ لَمْ يَلِدْ وَلَمْ يُولَدْ ۝ وَلَمْ يَكُنْ لَهُ كُفُوًا أَحَدٌ",
                "Surat al-Ikhlas — Say: He is Allah, the One; Allah, the Eternal Refuge; He neither begets nor is born, and there is none comparable to Him.",
                3, "القرآن الكريم", "الإخلاص: ١-٤", HadithGrade.QuranVerse, null),

            ("morning",
                "قُلْ أَعُوذُ بِرَبِّ الفَلَقِ ۝ مِنْ شَرِّ مَا خَلَقَ ۝ وَمِنْ شَرِّ غَاسِقٍ إِذَا وَقَبَ ۝ وَمِنْ شَرِّ النَّفَّاثَاتِ فِي العُقَدِ ۝ وَمِنْ شَرِّ حَاسِدٍ إِذَا حَسَدَ",
                "Surat al-Falaq — Say: I seek refuge in the Lord of daybreak, from the evil of what He has created…",
                3, "القرآن الكريم", "الفلق: ١-٥", HadithGrade.QuranVerse, null),

            ("morning",
                "قُلْ أَعُوذُ بِرَبِّ النَّاسِ ۝ مَلِكِ النَّاسِ ۝ إِلَهِ النَّاسِ ۝ مِنْ شَرِّ الوَسْوَاسِ الخَنَّاسِ ۝ الَّذِي يُوَسْوِسُ فِي صُدُورِ النَّاسِ ۝ مِنَ الجِنَّةِ وَالنَّاسِ",
                "Surat an-Nas — Say: I seek refuge in the Lord of mankind, the King of mankind, the God of mankind, from the evil of the retreating whisperer…",
                3, "القرآن الكريم", "الناس: ١-٦", HadithGrade.QuranVerse, null),

            ("morning",
                "سُبْحَانَ اللهِ",
                "Glory be to Allah.",
                100, "صحيح الترغيب", "٦٥٨", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "الحَمْدُ لِلَّهِ",
                "Praise belongs to Allah.",
                100, "صحيح الترغيب", "٦٥٧", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "اللهُ أَكْبَرُ",
                "Allah is the greatest.",
                100, "صحيح الترغيب", "٦٥٧", HadithGrade.Sahih, "الألباني"),

            // Said in the morning only.
            ("morning",
                "رَضِيتُ بِاللهِ رَبًّا، وَبِالإِسْلَامِ دِينًا، وَبِمُحَمَّدٍ ﷺ نَبِيًّا",
                "I am content with Allah as Lord, with Islam as religion, and with Muhammad (peace be upon him) as Prophet.",
                3, "السلسلة الصحيحة", "٢٦٨٦", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "أَصْبَحْنَا عَلَى فِطْرَةِ الإِسْلَامِ، وَكَلِمَةِ الإِخْلَاصِ، وَدِينِ نَبِيِّنَا مُحَمَّدٍ ﷺ، وَمِلَّةِ أَبِينَا إِبْرَاهِيمَ حَنِيفًا مُسْلِمًا وَمَا كَانَ مِنَ المُشْرِكِينَ",
                "We have entered the morning upon the natural way of Islam, the word of sincerity, the religion of our Prophet Muhammad (peace be upon him), and the creed of our father Abraham — upright, submitting, and he was not of those who associate partners with Allah.",
                1, "السلسلة الصحيحة", "١٦٠٠", HadithGrade.Sahih, "الألباني"),

            ("morning",
                "أَسْتَغْفِرُ اللهَ وَأَتُوبُ إِلَيْهِ",
                "I seek Allah's forgiveness and turn to Him in repentance.",
                100, "صحيح البخاري", "٦٣٠٧", HadithGrade.Sahih, null),

            ("morning",
                "سُبْحَانَ اللهِ وَبِحَمْدِهِ، عَدَدَ خَلْقِهِ، وَرِضَا نَفْسِهِ، وَزِنَةَ عَرْشِهِ، وَمِدَادَ كَلِمَاتِهِ",
                "Glory be to Allah and praise be to Him, as many as the number of His creation, as pleases Him, as weighs His throne, and as endless as the ink of His words.",
                3, "صحيح مسلم", "٢٧٢٦", HadithGrade.Sahih, null),

            ("evening",
                "أَمْسَيْنَا وَأَمْسَى المُلْكُ لِلَّهِ، وَالحَمْدُ لِلَّهِ، لَا إِلَهَ إِلَّا اللهُ وَحْدَهُ لَا شَرِيكَ لَهُ، لَهُ المُلْكُ وَلَهُ الحَمْدُ وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ، رَبِّ أَسْأَلُكَ خَيْرَ مَا فِي هَذِهِ اللَّيْلَةِ وَخَيْرَ مَا بَعْدَهَا، وَأَعُوذُ بِكَ مِنْ شَرِّ مَا فِي هَذِهِ اللَّيْلَةِ وَشَرِّ مَا بَعْدَهَا، رَبِّ أَعُوذُ بِكَ مِنَ الكَسَلِ وَسُوءِ الكِبَرِ، رَبِّ أَعُوذُ بِكَ مِنْ عَذَابٍ فِي النَّارِ وَعَذَابٍ فِي القَبْرِ",
                "We have entered the evening and the dominion belongs to Allah; praise belongs to Allah. There is no god but Allah alone, without partner; His is the dominion and His the praise, and He is able to do all things. My Lord, I ask You for the good of this night and the good of what follows it, and I seek refuge in You from the evil of this night and the evil of what follows it. My Lord, I seek refuge in You from idleness and the wretchedness of old age. My Lord, I seek refuge in You from punishment in the Fire and punishment in the grave.",
                1, "صحيح مسلم", "٢٧٢٣", HadithGrade.Sahih, null),

            ("evening",
                "اللَّهُمَّ بِكَ أَمْسَيْنَا، وَبِكَ أَصْبَحْنَا، وَبِكَ نَحْيَا، وَبِكَ نَمُوتُ، وَإِلَيْكَ المَصِيرُ",
                "O Allah, by You we enter the evening and by You we enter the morning; by You we live and by You we die, and to You is the return.",
                1, "السلسلة الصحيحة", "٢٦٢", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "اللَّهُمَّ أَنْتَ رَبِّي لَا إِلَهَ إِلَّا أَنْتَ، خَلَقْتَنِي وَأَنَا عَبْدُكَ، وَأَنَا عَلَى عَهْدِكَ وَوَعْدِكَ مَا اسْتَطَعْتُ، أَعُوذُ بِكَ مِنْ شَرِّ مَا صَنَعْتُ، أَبُوءُ لَكَ بِنِعْمَتِكَ عَلَيَّ، وَأَبُوءُ لَكَ بِذَنْبِي فَاغْفِرْ لِي، فَإِنَّهُ لَا يَغْفِرُ الذُّنُوبَ إِلَّا أَنْتَ",
                "O Allah, You are my Lord; there is no god but You. You created me and I am Your servant, and I hold to Your covenant and Your promise as much as I am able. I seek refuge in You from the evil of what I have done. I acknowledge Your favour upon me and I acknowledge my sin, so forgive me — for none forgives sins but You. (The chief manner of seeking forgiveness.)",
                1, "صحيح البخاري", "٦٣٠٦", HadithGrade.Sahih, null),

            ("evening",
                "اللَّهُمَّ عَالِمَ الغَيْبِ وَالشَّهَادَةِ، فَاطِرَ السَّمَاوَاتِ وَالأَرْضِ، رَبَّ كُلِّ شَيْءٍ وَمَلِيكَهُ، أَشْهَدُ أَنْ لَا إِلَهَ إِلَّا أَنْتَ، أَعُوذُ بِكَ مِنْ شَرِّ نَفْسِي، وَشَرِّ الشَّيْطَانِ وَشِرْكِهِ، وَأَنْ أَقْتَرِفَ عَلَى نَفْسِي سُوءًا أَوْ أَجُرَّهُ إِلَى مُسْلِمٍ",
                "O Allah, Knower of the unseen and the seen, Originator of the heavens and the earth, Lord and Sovereign of all things: I bear witness that there is no god but You. I seek refuge in You from the evil of my own self, and from the evil of Satan and his incitement to idolatry, and from bringing evil upon myself or drawing it upon a Muslim.",
                1, "صحيح الكلم الطيب", "٢٢", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "يَا حَيُّ يَا قَيُّومُ بِرَحْمَتِكَ أَسْتَغِيثُ، أَصْلِحْ لِي شَأْنِي كُلَّهُ، وَلَا تَكِلْنِي إِلَى نَفْسِي طَرْفَةَ عَيْنٍ",
                "O Living, O Sustaining One, by Your mercy I seek relief. Set right all my affairs, and do not entrust me to myself for the blink of an eye.",
                1, "صحيح الترغيب", "٦٦١", HadithGrade.Hasan, "الألباني"),

            ("evening",
                "اللَّهُمَّ إِنِّي أَسْأَلُكَ العَافِيَةَ فِي الدُّنْيَا وَالآخِرَةِ، اللَّهُمَّ إِنِّي أَسْأَلُكَ العَفْوَ وَالعَافِيَةَ فِي دِينِي وَدُنْيَايَ وَأَهْلِي وَمَالِي، اللَّهُمَّ اسْتُرْ عَوْرَاتِي وَآمِنْ رَوْعَاتِي، اللَّهُمَّ احْفَظْنِي مِنْ بَيْنِ يَدَيَّ وَمِنْ خَلْفِي، وَعَنْ يَمِينِي وَعَنْ شِمَالِي، وَمِنْ فَوْقِي، وَأَعُوذُ بِعَظَمَتِكَ أَنْ أُغْتَالَ مِنْ تَحْتِي",
                "O Allah, I ask You for well-being in this world and the next. O Allah, I ask You for pardon and well-being in my religion, my worldly life, my family and my wealth. O Allah, conceal my faults and calm my fears. O Allah, guard me from before me and behind me, from my right and my left, and from above me; and I seek refuge in Your greatness from being taken unawares from beneath me.",
                1, "صحيح الكلم الطيب", "٢٧", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "بِسْمِ اللهِ الَّذِي لَا يَضُرُّ مَعَ اسْمِهِ شَيْءٌ فِي الأَرْضِ وَلَا فِي السَّمَاءِ وَهُوَ السَّمِيعُ العَلِيمُ",
                "In the name of Allah, with whose name nothing on earth or in heaven can cause harm, and He is the All-Hearing, the All-Knowing.",
                3, "صحيح الترمذي", "٣٣٨٨", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "اللَّهُمَّ عَافِنِي فِي بَدَنِي، اللَّهُمَّ عَافِنِي فِي سَمْعِي، اللَّهُمَّ عَافِنِي فِي بَصَرِي، لَا إِلَهَ إِلَّا أَنْتَ. اللَّهُمَّ إِنِّي أَعُوذُ بِكَ مِنَ الكُفْرِ وَالفَقْرِ، اللَّهُمَّ إِنِّي أَعُوذُ بِكَ مِنْ عَذَابِ القَبْرِ، لَا إِلَهَ إِلَّا أَنْتَ",
                "O Allah, grant me well-being in my body. O Allah, grant me well-being in my hearing. O Allah, grant me well-being in my sight. There is no god but You. O Allah, I seek refuge in You from disbelief and poverty, and I seek refuge in You from the punishment of the grave. There is no god but You.",
                3, "صحيح أبي داود", "٥٠٩٠", HadithGrade.Hasan, "الألباني"),

            ("evening",
                "لَا إِلَهَ إِلَّا اللهُ وَحْدَهُ لَا شَرِيكَ لَهُ، لَهُ المُلْكُ وَلَهُ الحَمْدُ وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ",
                "There is no god but Allah alone, without partner; His is the dominion and His the praise, and He is able to do all things.",
                100, "صحيح أبي داود", "٥٠٧٧", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "لَا إِلَهَ إِلَّا اللهُ وَحْدَهُ لَا شَرِيكَ لَهُ، لَهُ المُلْكُ وَلَهُ الحَمْدُ، يُحْيِي وَيُمِيتُ، وَهُوَ عَلَى كُلِّ شَيْءٍ قَدِيرٌ",
                "There is no god but Allah alone, without partner; His is the dominion and His the praise. He gives life and causes death, and He is able to do all things.",
                10, "السلسلة الصحيحة", "٢٧٦٢", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "سُبْحَانَ اللهِ وَبِحَمْدِهِ",
                "Glory be to Allah, and praise be to Him.",
                100, "صحيح مسلم", "٢٦٩٢", HadithGrade.Sahih, null),

            ("evening",
                "اللهُ لَا إِلَهَ إِلَّا هُوَ الحَيُّ القَيُّومُ، لَا تَأْخُذُهُ سِنَةٌ وَلَا نَوْمٌ، لَهُ مَا فِي السَّمَاوَاتِ وَمَا فِي الأَرْضِ، مَنْ ذَا الَّذِي يَشْفَعُ عِنْدَهُ إِلَّا بِإِذْنِهِ، يَعْلَمُ مَا بَيْنَ أَيْدِيهِمْ وَمَا خَلْفَهُمْ، وَلَا يُحِيطُونَ بِشَيْءٍ مِنْ عِلْمِهِ إِلَّا بِمَا شَاءَ، وَسِعَ كُرْسِيُّهُ السَّمَاوَاتِ وَالأَرْضَ، وَلَا يَئُودُهُ حِفْظُهُمَا، وَهُوَ العَلِيُّ العَظِيمُ",
                "Ayat al-Kursi — Allah, there is no god but He, the Living, the Sustainer of all. Neither drowsiness nor sleep overtakes Him. To Him belongs whatever is in the heavens and whatever is on the earth…",
                1, "القرآن الكريم", "البقرة: ٢٥٥", HadithGrade.QuranVerse, null),

            ("evening",
                "قُلْ هُوَ اللهُ أَحَدٌ ۝ اللهُ الصَّمَدُ ۝ لَمْ يَلِدْ وَلَمْ يُولَدْ ۝ وَلَمْ يَكُنْ لَهُ كُفُوًا أَحَدٌ",
                "Surat al-Ikhlas — Say: He is Allah, the One; Allah, the Eternal Refuge; He neither begets nor is born, and there is none comparable to Him.",
                3, "القرآن الكريم", "الإخلاص: ١-٤", HadithGrade.QuranVerse, null),

            ("evening",
                "قُلْ أَعُوذُ بِرَبِّ الفَلَقِ ۝ مِنْ شَرِّ مَا خَلَقَ ۝ وَمِنْ شَرِّ غَاسِقٍ إِذَا وَقَبَ ۝ وَمِنْ شَرِّ النَّفَّاثَاتِ فِي العُقَدِ ۝ وَمِنْ شَرِّ حَاسِدٍ إِذَا حَسَدَ",
                "Surat al-Falaq — Say: I seek refuge in the Lord of daybreak, from the evil of what He has created…",
                3, "القرآن الكريم", "الفلق: ١-٥", HadithGrade.QuranVerse, null),

            ("evening",
                "قُلْ أَعُوذُ بِرَبِّ النَّاسِ ۝ مَلِكِ النَّاسِ ۝ إِلَهِ النَّاسِ ۝ مِنْ شَرِّ الوَسْوَاسِ الخَنَّاسِ ۝ الَّذِي يُوَسْوِسُ فِي صُدُورِ النَّاسِ ۝ مِنَ الجِنَّةِ وَالنَّاسِ",
                "Surat an-Nas — Say: I seek refuge in the Lord of mankind, the King of mankind, the God of mankind, from the evil of the retreating whisperer…",
                3, "القرآن الكريم", "الناس: ١-٦", HadithGrade.QuranVerse, null),

            ("evening",
                "سُبْحَانَ اللهِ",
                "Glory be to Allah.",
                100, "صحيح الترغيب", "٦٥٨", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "الحَمْدُ لِلَّهِ",
                "Praise belongs to Allah.",
                100, "صحيح الترغيب", "٦٥٧", HadithGrade.Sahih, "الألباني"),

            ("evening",
                "اللهُ أَكْبَرُ",
                "Allah is the greatest.",
                100, "صحيح الترغيب", "٦٥٧", HadithGrade.Sahih, "الألباني"),

            // Said in the evening only.
            ("evening",
                "أَعُوذُ بِكَلِمَاتِ اللهِ التَّامَّاتِ مِنْ شَرِّ مَا خَلَقَ",
                "I seek refuge in the perfect words of Allah from the evil of what He has created.",
                3, "صحيح مسلم", "٢٧٠٩", HadithGrade.Sahih, null),

            ("sleep",
                "بِاسْمِكَ اللَّهُمَّ أَمُوتُ وَأَحْيَا",
                "In Your name, O Allah, I die and I live.",
                1, "صحيح البخاري", "٦٣٢٤", HadithGrade.Sahih, null),

            ("waking",
                "الحَمْدُ لِلَّهِ الَّذِي أَحْيَانَا بَعْدَ مَا أَمَاتَنَا وَإِلَيْهِ النُّشُورُ",
                "Praise belongs to Allah, who gave us life after He had caused us to die, and to Him is the return.",
                1, "صحيح البخاري", "٦٣٢٥", HadithGrade.Sahih, null),

            ("after-prayer",
                "سُبْحَانَ اللهِ",
                "Glory be to Allah.",
                33, "صحيح مسلم", "٥٩٧", HadithGrade.Sahih, null),

            ("after-prayer",
                "الحَمْدُ لِلَّهِ",
                "Praise belongs to Allah.",
                33, "صحيح مسلم", "٥٩٧", HadithGrade.Sahih, null),

            ("after-prayer",
                "اللهُ أَكْبَرُ",
                "Allah is the greatest.",
                33, "صحيح مسلم", "٥٩٧", HadithGrade.Sahih, null),

            ("food",
                "بِسْمِ اللهِ",
                "In the name of Allah.",
                1, "سنن أبي داود", "٣٧٦٧", HadithGrade.Sahih, "الألباني"),

            ("distress",
                "لَا إِلَهَ إِلَّا اللهُ العَظِيمُ الحَلِيمُ، لَا إِلَهَ إِلَّا اللهُ رَبُّ العَرْشِ العَظِيمِ",
                "There is no god but Allah, the Mighty, the Forbearing; there is no god but Allah, Lord of the Mighty Throne.",
                1, "صحيح البخاري", "٦٣٤٥", HadithGrade.MuttafaqAlayh, null),

            ("istighfar",
                "أَسْتَغْفِرُ اللهَ وَأَتُوبُ إِلَيْهِ",
                "I seek Allah's forgiveness and turn to Him in repentance.",
                100, "صحيح مسلم", "٢٧٠٢", HadithGrade.Sahih, null),

            ("salat-nabi",
                "اللَّهُمَّ صَلِّ عَلَى مُحَمَّدٍ وَعَلَى آلِ مُحَمَّدٍ",
                "O Allah, send blessings upon Muhammad and upon the family of Muhammad.",
                10, "صحيح البخاري", "٣٣٧٠", HadithGrade.Sahih, null),
        };

        var order = new Dictionary<string, int>();

        foreach (var seed in seeds)
        {
            var category = byKey[seed.Category];
            var sortOrder = order.GetValueOrDefault(seed.Category);
            order[seed.Category] = sortOrder + 1;

            var dhikr = new Dhikr
            {
                CategoryId = category.Id,
                SortOrder = sortOrder,
                ArabicText = seed.Arabic,
                SearchText = ArabicText.Normalize(seed.Arabic),
                RepeatCount = seed.Repeat,
                SourceBook = seed.Book,
                SourceReference = seed.Reference,
                Grade = seed.Grade,
                GradedBy = seed.GradedBy,
                IsPublished = true,
            };
            dhikr.Translations.Add(new DhikrTranslation
            {
                LanguageCode = "en",
                Translation = seed.English,
            });

            db.Adhkar.Add(dhikr);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The two reminders every reader expects to exist, both device-local and
    /// both anchored — which is the arrangement the whole design turns on, so
    /// the seeder demonstrates it rather than leaving the CMS to discover it.
    /// </summary>
    private static async Task SeedReminders(DatabaseService db)
    {
        if (await db.ReminderCampaigns.AnyAsync()) return;

        var morning = await db.Categories.FirstOrDefaultAsync(c => c.Key == "morning");
        var evening = await db.Categories.FirstOrDefaultAsync(c => c.Key == "evening");

        var campaigns = new[]
        {
            Build("morning-adhkar", morning?.Id, PrayerAnchor.Sunrise, 30,
                "أذكار الصباح", "حان وقت أذكار الصباح",
                "Morning remembrance", "It is time for the morning adhkar"),

            Build("evening-adhkar", evening?.Id, PrayerAnchor.Asr, 30,
                "أذكار المساء", "حان وقت أذكار المساء",
                "Evening remembrance", "It is time for the evening adhkar"),
        };

        db.ReminderCampaigns.AddRange(campaigns);
        await db.SaveChangesAsync();

        static ReminderCampaign Build(string key, int? categoryId, PrayerAnchor anchor, int offset,
            string titleAr, string bodyAr, string titleEn, string bodyEn)
        {
            var campaign = new ReminderCampaign
            {
                Key = key,
                CategoryId = categoryId,
                Kind = ReminderKind.PrayerAnchored,
                Delivery = ReminderDelivery.DeviceLocal,
                Anchor = anchor,
                OffsetMinutes = offset,
                Days = WeekDays.All,
                Audience = Audience.All,
                AndroidChannelId = PushRules.Channels.Reminders,
                IsEnabled = true,
                IsUserAdjustable = true,
            };

            campaign.Translations.Add(new ReminderCampaignTranslation
            {
                LanguageCode = "ar", Title = titleAr, Body = bodyAr,
            });
            campaign.Translations.Add(new ReminderCampaignTranslation
            {
                LanguageCode = "en", Title = titleEn, Body = bodyEn,
            });

            return campaign;
        }
    }

    /// <summary>
    /// The questions this app actually gets asked.
    ///
    /// Every answer here is a statement about how the code behaves, not
    /// marketing copy: the reminders answer describes what
    /// <c>ReminderScheduler</c> does, the location answer describes what the
    /// server is capable of knowing, and the battery answer is the single most
    /// asked question an app of this kind receives
    /// (<c>docs/BUSINESS_LOGIC.md</c> §5). When one of those behaviours changes,
    /// this text is wrong and has to change with it — an FAQ that describes a
    /// previous version of the app is worse than no FAQ, because a reader
    /// believes it and stops looking.
    ///
    /// Guarded like the rest: an editor who deleted an entry meant to.
    /// </summary>
    private static async Task SeedFaq(DatabaseService db)
    {
        if (await db.FaqItems.AnyAsync()) return;

        var faq = new (FaqCategory Category, string QuestionAr, string AnswerAr, string QuestionEn, string AnswerEn)[]
        {
            // ── General ──
            (FaqCategory.General,
                "ما هو تطبيق أذكاري؟",
                "أذكاري تطبيق للأذكار ومواقيت الصلاة والقبلة، وهو وقفٌ لله تعالى: مجاني بالكامل، بلا إعلانات، وبلا اشتراكات. ليس فيه ما يُباع ولا ما يُشترى.",
                "What is Athkari?",
                "Athkari is an app for adhkar, prayer times and the qibla, offered as an endowment (waqf): entirely free, with no advertising and no subscriptions. There is nothing in it to buy."),

            (FaqCategory.General,
                "هل أحتاج إلى إنشاء حساب أو تسجيل الدخول؟",
                "لا. لا يوجد في التطبيق تسجيل ولا كلمة مرور ولا بريد إلكتروني ولا رقم هاتف. تفتح التطبيق فتجده يعمل. تسجيل الدخول موجود للعاملين على المحتوى في لوحة التحكم فقط.",
                "Do I need an account to use the app?",
                "No. There is no sign-up, no password, no email address and no phone number. You open the app and it works. Signing in exists only for the content team, in the control panel."),

            (FaqCategory.General,
                "هل يعمل التطبيق بدون إنترنت؟",
                "نعم. الأذكار ومواقيت الصلاة والقبلة تعمل كلها دون اتصال. التطبيق يفتح على ما هو محفوظ عنده ثم يحاول التحديث في الخلفية؛ فإن تعذّر الاتصال لم يتغيّر شيء مما تراه.",
                "Does the app work offline?",
                "Yes. The adhkar, the prayer times and the qibla all work with no connection. The app opens on what it already has stored and only then tries to refresh in the background; if that fails, nothing you can see changes."),

            (FaqCategory.General,
                "كيف أغيّر لغة التطبيق؟",
                "من الإعدادات ← اللغة. التطبيق متاح بالعربية والإنجليزية، ونصوص الأذكار تبقى بالعربية في كل الأحوال لأنها النص المَرْوِي نفسه، وما دونها ترجمة له.",
                "How do I change the app's language?",
                "Settings → Language. The app is available in Arabic and English. The adhkar themselves stay in Arabic whichever you choose — that is the narrated text itself, and everything else is a translation of it."),

            // ── Adhkar ──
            (FaqCategory.Adhkar,
                "من أين تأتي الأذكار الموجودة في التطبيق؟",
                "كل ذكر في التطبيق منسوب إلى كتابٍ ورقمٍ ودرجة، وتجد ذلك أسفل كل ذكر. والتطبيق لا يعرض ذكرًا بلا مصدر: لا يُنشر شيء إلا بعد إثبات مصدره، وهذا شرط في البرنامج نفسه لا مجرد سياسة تحرير.",
                "Where do the adhkar in this app come from?",
                "Every dhikr is attributed to a book, a number and a grading, shown beneath it. The app does not display a dhikr without a source: nothing can be published until its source is recorded, and that is enforced by the software itself rather than left to editorial policy."),

            (FaqCategory.Adhkar,
                "ماذا تعني الدرجات المكتوبة تحت الذكر؟",
                "«صحيح» و«حسن» درجتان في ثبوت الحديث، و«متفق عليه» تعني أن البخاري ومسلمًا روياه جميعًا، و«صحيح لغيره» و«حسن لغيره» يعنيان أن الحديث بلغ درجته بمجموع طرقه. وما كان من القرآن كُتب بالسورة والآية لأنه لا سند له يُحكم عليه. والتطبيق لا يعرض ما دون درجة الحسن.",
                "What do the gradings under a dhikr mean?",
                "“Sahih” and “Hasan” are two degrees of authenticity. “Muttafaq alayh” means both al-Bukhari and Muslim narrate it. “Sahih li-ghayrihi” and “Hasan li-ghayrihi” mean the report reaches its grade once its supporting chains are counted. Qur'anic text is cited by surah and ayah instead, since it has no chain to grade. Nothing below Hasan is shown."),

            (FaqCategory.Adhkar,
                "من أصدر هذه الأحكام على الأحاديث؟",
                "اسم من حكم على الحديث مكتوب مع الدرجة كلما كان الحكم منسوبًا إلى عالم بعينه، مثل «الألباني». والتطبيق لا يُصدر أحكامًا على الأسانيد من عند نفسه؛ إنما ينقل حكم أهله وينسبه إليهم.",
                "Who issued these gradings?",
                "Where a grading belongs to a particular scholar, that scholar's name is recorded alongside it — “al-Albani”, for instance. The app does not grade chains of narration on its own authority; it reports the ruling and names whose ruling it is."),

            (FaqCategory.Adhkar,
                "بحثتُ عن ذكر أحفظه فلم أجده، فما السبب؟",
                "البحث يتجاهل التشكيل وصور الحروف، فلا فرق عنده بين «أ» و«ا»، ولا بين «ة» و«ه»، ولا يضرّك ترك الحركات. جرّب كلمتين متتاليتين من وسط الذكر بدل أوله. فإن لم تجده فأرسل إلينا عبر «تواصل معنا».",
                "I searched for a dhikr I know and could not find it — why?",
                "Search ignores diacritics and letter shapes, so alif forms and ta-marbuta versus ha make no difference and you need not type a single vowel mark. Try two consecutive words from the middle of the text rather than the beginning. If it is genuinely missing, tell us through “Contact us”."),

            (FaqCategory.Adhkar,
                "وجدتُ خطأً في نصّ ذكر أو في عزوه، فماذا أفعل؟",
                "أخبرنا من «تواصل معنا» في التطبيق، واذكر الذكر والموضع الذي تراه خطأً وما تراه صوابًا. هذا أنفع ما تصنعه لنا: صحة النص هي الشيء الوحيد الذي يقوم عليه التطبيق كله.",
                "I found an error in a text or its attribution. What should I do?",
                "Tell us through “Contact us” in the app: name the dhikr, the place you believe is wrong, and what you believe is correct. This is the most useful thing you can send us — the accuracy of the text is the one thing this whole app rests on."),

            // ── Prayer times ──
            (FaqCategory.PrayerTimes,
                "مواقيت الصلاة عندي تخالف مسجد الحيّ بدقائق، فكيف أضبطها؟",
                "من الإعدادات ← مواقيت الصلاة ← التعديل اليدوي، ولك أن تزيد أو تنقص لكل صلاة على حدة إلى ستين دقيقة. التعديل يُضاف فوق طريقة الحساب المختارة، ويبقى محفوظًا، وتُعاد جدولة التذكيرات عليه.",
                "The times differ from my local mosque by a few minutes. Can I correct them?",
                "Settings → Prayer times → Manual adjustment. Each prayer can be moved up to sixty minutes either way. The adjustment sits on top of your chosen calculation method, is remembered, and your reminders are rescheduled around it."),

            (FaqCategory.PrayerTimes,
                "كيف أختار طريقة الحساب والمذهب؟",
                "من الإعدادات ← مواقيت الصلاة. طريقة الحساب تحدد زاويتي الفجر والعشاء، والمذهب يحدد وقت العصر (الجمهور أو الحنفي). ويبدأ التطبيق بما هو شائع في منطقتك، ثم الاختيار لك بعد ذلك.",
                "How do I choose the calculation method and madhab?",
                "Settings → Prayer times. The method sets the Fajr and Isha angles; the madhab decides when Asr begins (the majority position or the Hanafi one). The app starts from what is customary where you are, and the choice is yours from then on."),

            (FaqCategory.PrayerTimes,
                "هل يلزمني السماح بتحديد الموقع؟",
                "لا. يمكنك اختيار مدينتك من قائمة المدن المدرجة في التطبيق، ولن يُطلب منك إذن الموقع إلا إذا ضغطت «استخدم موقعي» بنفسك. وإن لم تحدد موقعًا ولا مدينة اختفى قسم المواقيت وبقي سائر التطبيق يعمل.",
                "Do I have to grant location permission?",
                "No. You can pick your city from the list the app ships with, and the location prompt appears only if you tap “use my location” yourself. With no location and no city, the prayer section is simply absent and everything else still works."),

            (FaqCategory.PrayerTimes,
                "التاريخ الهجري يسبق أو يتأخر يومًا واحدًا.",
                "من الإعدادات ← التقويم، وفيه تعديل بمقدار يوم زيادة أو نقصانًا. والرؤية تختلف من بلد إلى بلد، فلا يصح للتطبيق أن يُلزم الناس بجواب حسابيّ واحد.",
                "The Hijri date is a day ahead or behind.",
                "Settings → Calendar has a ±1 day adjustment. Moon sighting differs from country to country, so it would be wrong for the app to insist on a single arithmetic answer."),

            // ── Notifications ──
            (FaqCategory.Notifications,
                "لماذا لا تصلني التذكيرات، أو تصل متأخرة؟",
                "على أجهزة أندرويد من شاومي وأوبو وهواوي وفيفو وسامسونج غالبًا، يوقف «مدير البطارية» التطبيقات في الخلفية ويمنع المنبّهات الدقيقة من العمل. والحل أن تستثني أذكاري من توفير البطارية وتسمح له بالعمل في الخلفية؛ وفي التطبيق شاشة «لا تصلني التذكيرات» تشرح الخطوات لكل شركة على حدة. هذا سبب المشكلة في أغلب الحالات، وليس في قدرة أي تطبيق تجاوزه.",
                "Why don't my reminders arrive, or arrive late?",
                "On Android — especially Xiaomi, Oppo, Huawei, Vivo and often Samsung — the manufacturer's battery manager stops background apps and prevents exact alarms from firing. The fix is to exempt Athkari from battery optimisation and allow it to run in the background; the app has a “My reminders don't arrive” screen with the steps for each manufacturer. This is the cause in the large majority of cases, and no app can work around it."),

            (FaqCategory.Notifications,
                "كيف تعرف التذكيرات المرتبطة بالصلاة وقتها؟",
                "يحسبها جهازك، لا الخادم. مواقيت الصلاة تُحسب على الجهاز من موقعك، والخادم لا يعرف موقعك أصلًا، فلا يستطيع أن يعرف متى مغربك. ولهذا يجدول الهاتف هذه التذكيرات بنفسه، وهي تعمل من غير إنترنت.",
                "How do prayer-linked reminders know when to fire?",
                "Your phone works it out, not the server. Prayer times are computed on the device from your location, and the server never learns your location, so it cannot know when your Maghrib is. The phone therefore schedules these reminders itself — which is also why they work with no connection."),

            (FaqCategory.Notifications,
                "غيّرتُ صوت التنبيه ولم يتغيّر شيء.",
                "أندرويد يثبّت خصائص «قناة الإشعارات» عند إنشائها أول مرة فلا تقبل التعديل بعدها. فإن لم يتغير الصوت فغيّره من إعدادات النظام ← الإشعارات ← أذكاري، واختر القناة المعنية. وقد يحتاج التغيير الجذري إلى تحديث للتطبيق يُنشئ قناة جديدة.",
                "I changed the notification sound and nothing changed.",
                "Android fixes a notification channel's properties when the channel is first created, and they cannot be edited afterwards. Change the sound from your system Settings → Notifications → Athkari and pick the relevant channel. A deeper change may need an app update that creates a new channel."),

            (FaqCategory.Notifications,
                "كيف أوقف تذكيرًا واحدًا دون البقية؟",
                "من التطبيق ← التذكيرات، ولكل تذكير مفتاح خاص به، وبعضها يقبل تقديم وقته أو تأخيره. وإيقاف تذكير لا يؤثر في غيره ولا يُلغي الإشعارات كلها.",
                "How do I turn off one reminder without the others?",
                "Open Reminders in the app: each has its own switch, and some can be moved earlier or later. Muting one does not affect the rest and does not disable notifications as a whole."),

            // ── Qibla ──
            (FaqCategory.Qibla,
                "هل البوصلة تشير إلى الشمال المغناطيسي أم الحقيقي؟",
                "اتجاه القبلة يُحسب من الشمال الحقيقي، وبوصلة الهاتف تقرأ الشمال المغناطيسي، والفرق بينهما يبلغ خمس درجات في إسطنبول وأكثر من خمس عشرة على الساحل الشرقي لأمريكا. والتطبيق يصحّح هذا الفرق معتمدًا على ما يوفّره نظام الهاتف نفسه، لا على تقدير من عنده.",
                "Does the compass point to magnetic or true north?",
                "The qibla bearing is computed from true north, while a phone's compass reads magnetic north — a difference of about 5° in Istanbul and over 15° on the American east coast. The app corrects for it using the figure the phone's own platform provides, rather than an approximation of its own."),

            (FaqCategory.Qibla,
                "البوصلة لا تتحرك أو تتذبذب.",
                "إن كان جهازك بلا حسّاس مغناطيسي فلن تتحرك البوصلة، ويعرض التطبيق حينئذٍ اتجاه القبلة رقمًا بالدرجات بدل بوصلة واقفة. وإن كانت تتذبذب فأبعِد الهاتف عن المعادن والشواحن وحرّكه في الهواء على هيئة الرقم ثمانية لتُعاير البوصلة.",
                "The compass doesn't move, or it wobbles.",
                "If your device has no magnetometer the dial cannot move, so the app shows the qibla as a bearing in degrees instead of a compass that never turns. If it wobbles, move away from metal and chargers and trace a figure eight in the air to recalibrate."),

            // ── Qur'an ──
            (FaqCategory.Quran,
                "كيف أُنزّل المصحف؟",
                "من قسم القرآن، ويُنزَّل ملفًا واحدًا مرة واحدة. والتنزيل يستأنف من حيث انقطع إن انقطع، ويتحقق التطبيق من سلامة الملف قبل اعتماده، فلا يُعتمد مصحف ناقص.",
                "How do I download the mushaf?",
                "From the Qur'an section. It downloads once, as a single file. An interrupted download resumes where it stopped, and the app verifies the file's integrity before putting it in place, so a truncated mushaf is never accepted."),

            (FaqCategory.Quran,
                "هل أستطيع القراءة بعد التنزيل دون إنترنت؟",
                "نعم، المصحف كله على جهازك بعد التنزيل، ولا يحتاج إلى اتصال. ولا يُطلب منك التنزيل ثانية إلا إذا صدرت نسخة أحدث.",
                "Can I read offline after downloading?",
                "Yes — the whole mushaf is on your device and needs no connection. You will only be asked to download again if a newer edition is released."),

            // ── Privacy ──
            (FaqCategory.Privacy,
                "هل تُرسَل إحداثيات موقعي إلى الخادم؟",
                "لا، ولا مرة. موقعك لا يفارق جهازك، ومواقيت الصلاة والقبلة تُحسب عليه. الخادم لا يملك عمودًا يخزّن فيه إحداثياتك أصلًا.",
                "Are my coordinates sent to your server?",
                "No — not once. Your location never leaves your device; prayer times and the qibla are computed on it. The server has no column in which to store your coordinates in the first place."),

            (FaqCategory.Privacy,
                "ما البيانات التي يجمعها التطبيق؟",
                "لا حسابات ولا إعلانات ولا تتبّع تحليلي. وما يُحفظ على الخادم هو ما يلزم لإيصال الإشعارات إلى جهازك: رمز الإشعارات، ولغة الجهاز، ومنطقته الزمنية، ونوع نظامه. وليس في ذلك اسمك ولا بريدك ولا موقعك ولا ما تقرأ.",
                "What data does the app collect?",
                "No accounts, no advertising, no analytics tracking. What the server keeps is what is needed to deliver a notification to your device: a push token, the device's language, its time zone and its platform. None of that is your name, your email, your location, or what you read."),

            (FaqCategory.Privacy,
                "هل تعرفون ما قرأتُه من الأذكار؟",
                "لا. عدّاد الأذكار وسجلّ قراءتك محفوظان على جهازك وحده ولا يُرفعان إلى الخادم. وإذا حذفت التطبيق ذهبا معه، فلا توجد نسخة عندنا تُستعاد منها.",
                "Do you know which adhkar I have read?",
                "No. Your counter and your reading history live on your device alone and are never uploaded. If you delete the app they go with it — there is no copy on our side to restore from."),
        };

        var order = new Dictionary<FaqCategory, int>();

        foreach (var entry in faq)
        {
            var sortOrder = order.GetValueOrDefault(entry.Category);
            order[entry.Category] = sortOrder + 1;

            var item = new FaqItem
            {
                Category = entry.Category,
                SortOrder = sortOrder,
                IsPublished = true,
            };

            item.Translations.Add(new FaqTranslation
            {
                LanguageCode = "ar", Question = entry.QuestionAr, Answer = entry.AnswerAr,
            });
            item.Translations.Add(new FaqTranslation
            {
                LanguageCode = "en", Question = entry.QuestionEn, Answer = entry.AnswerEn,
            });

            db.FaqItems.Add(item);
        }

        await db.SaveChangesAsync();
    }
}
