package com.athkar.athkar_app

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
import android.view.View
import android.widget.RemoteViews

/**
 * The home-screen widget.
 *
 * ## Where its content comes from
 *
 * Everything it draws is written by the app into [PREFS] — see
 * [WidgetBridge] — and this class only renders. That split is deliberate:
 * a widget gets a fraction of a second and no network, so it can afford to read
 * five strings and lay them out, and nothing else. Re-deriving prayer times here
 * would mean a second implementation of the calculation to keep in step with
 * the Dart one, which is exactly how a widget ends up disagreeing with the app
 * that owns it.
 *
 * ## Why its own SharedPreferences file
 *
 * Not the Flutter plugin's store. That file's name, key prefixes and backing
 * implementation are the plugin's business and have changed between versions;
 * depending on them from Kotlin is a silent breakage waiting for an upgrade.
 * The app hands values across an explicit channel instead, and this file is the
 * contract.
 */
class AthkarWidget : AppWidgetProvider() {

    companion object {
        /** The store both halves agree on. Written by [WidgetBridge], read here. */
        const val PREFS = "athkar.widget"

        const val KEY_ENABLED = "enabled"
        const val KEY_KIND = "kind"
        const val KEY_THEME = "theme"
        const val KEY_DATE = "date"
        const val KEY_HEADLINE = "headline"
        const val KEY_TITLE = "title"
        const val KEY_SUBTITLE = "subtitle"
        const val KEY_PLACEHOLDER = "placeholder"

        /** Which gallery entry the reader chose. Diagnostic; the strings decide the layout. */
        const val KEY_WIDGET_KEY = "key"

        /**
         * The five prayers, as one string each.
         *
         * Joined on a unit separator rather than stored as a `StringSet`: a set
         * has no order, and the order here *is* the content — Fajr through
         * Isha. A launcher rendering them shuffled would be showing the reader
         * a timetable that is wrong in a way they would not notice at a glance.
         */
        const val KEY_PRAYER_LABELS = "prayerLabels"
        const val KEY_PRAYER_TIMES = "prayerTimes"

        /** Which column to pick out, or -1. The app decides; this side has no clock it can trust. */
        const val KEY_PRAYER_HIGHLIGHT = "prayerHighlight"

        /** Separates the joined lists above. Never appears in formatted text. */
        const val SEPARATOR = "\u001F"

        /** Repaints every placed widget. Called after the app writes new values. */
        fun refresh(context: Context) {
            val manager = AppWidgetManager.getInstance(context)
            val ids = manager.getAppWidgetIds(
                android.content.ComponentName(context, AthkarWidget::class.java)
            )

            if (ids.isEmpty()) return

            AthkarWidget().onUpdate(context, manager, ids)
        }
    }

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray,
    ) {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

        for (id in appWidgetIds) {
            appWidgetManager.updateAppWidget(id, render(context, prefs))
        }
    }

    private fun render(context: Context, prefs: SharedPreferences): RemoteViews {
        val views = RemoteViews(context.packageName, R.layout.athkar_widget)

        val enabled = prefs.getBoolean(KEY_ENABLED, true)
        val title = prefs.getString(KEY_TITLE, null)

        // Three states, and the middle one matters: the widget can be placed
        // before the app has ever run, in which case there is nothing to show
        // and saying so is better than an empty rectangle.
        // The placeholder itself is pushed by the app, so before the first
        // launch there is not one either — hence the string resource under it.
        val placeholder = prefs.getString(KEY_PLACEHOLDER, null)
            ?.takeIf { it.isNotBlank() }
            ?: context.getString(R.string.widget_not_ready)

        val body = when {
            !enabled -> placeholder
            title.isNullOrBlank() -> placeholder
            else -> title
        }

        val isPlaceholder = body != title

        applyTheme(context, views, prefs.getString(KEY_THEME, "system") ?: "system")

        views.setTextViewText(R.id.widget_title, body)
        views.setTextViewText(R.id.widget_date, prefs.getString(KEY_DATE, "") ?: "")
        views.setTextViewText(R.id.widget_headline, prefs.getString(KEY_HEADLINE, "") ?: "")
        views.setTextViewText(R.id.widget_subtitle, prefs.getString(KEY_SUBTITLE, "") ?: "")

        // A placeholder keeps only its sentence; the decorations around it would
        // be labelling nothing.
        val decorations = if (isPlaceholder) View.GONE else View.VISIBLE
        views.setViewVisibility(R.id.widget_headline, decorations)
        views.setViewVisibility(R.id.widget_subtitle, decorations)
        views.setViewVisibility(
            R.id.widget_date,
            if (isPlaceholder || (prefs.getString(KEY_DATE, "") ?: "").isEmpty()) View.GONE
            else View.VISIBLE,
        )

        applyPrayers(context, views, prefs, isPlaceholder)

        views.setOnClickPendingIntent(R.id.widget_root, launchIntent(context))

        return views
    }

    private val labelIds = intArrayOf(
        R.id.prayer_label_0, R.id.prayer_label_1, R.id.prayer_label_2,
        R.id.prayer_label_3, R.id.prayer_label_4,
    )

    private val timeIds = intArrayOf(
        R.id.prayer_time_0, R.id.prayer_time_1, R.id.prayer_time_2,
        R.id.prayer_time_3, R.id.prayer_time_4,
    )

    /**
     * Fills the five-column row, or hides it.
     *
     * Hidden whenever the app sent nothing, which covers three cases that all
     * look the same from here and should: the reader chose a widget that is not
     * about the timetable, they have set no location, or the app has never run.
     * The app promises to work without ever asking for a location, so an empty
     * row is a correct state rather than a missing one.
     */
    private fun applyPrayers(
        context: Context,
        views: RemoteViews,
        prefs: SharedPreferences,
        isPlaceholder: Boolean,
    ) {
        val labels = split(prefs.getString(KEY_PRAYER_LABELS, "") ?: "")
        val times = split(prefs.getString(KEY_PRAYER_TIMES, "") ?: "")

        if (isPlaceholder || labels.size != labelIds.size || times.size != timeIds.size) {
            views.setViewVisibility(R.id.widget_prayers, View.GONE)
            return
        }

        views.setViewVisibility(R.id.widget_prayers, View.VISIBLE)

        val dark = isDark(context, prefs.getString(KEY_THEME, "system") ?: "system")
        val ink = context.getColor(if (dark) R.color.widget_ink_dark else R.color.widget_ink_light)
        val muted =
            context.getColor(if (dark) R.color.widget_muted_dark else R.color.widget_muted_light)
        val brand =
            context.getColor(if (dark) R.color.widget_brand_dark else R.color.widget_brand_light)

        val highlight = prefs.getInt(KEY_PRAYER_HIGHLIGHT, -1)

        for (index in labelIds.indices) {
            views.setTextViewText(labelIds[index], labels[index])
            views.setTextViewText(timeIds[index], times[index])

            val isNext = index == highlight
            views.setTextColor(labelIds[index], if (isNext) brand else muted)
            views.setTextColor(timeIds[index], if (isNext) brand else ink)
        }
    }

    /** An empty string is an empty list, not a list holding one empty string. */
    private fun split(value: String): List<String> =
        if (value.isEmpty()) emptyList() else value.split(SEPARATOR)

    /**
     * Paints the widget.
     *
     * `system` follows the phone, which is what the launcher expects; the other
     * three are the admin overriding it, because a reader may run the app dark
     * and still want a light widget on a light wallpaper — and the launcher
     * gives no way to ask.
     */
    private fun applyTheme(context: Context, views: RemoteViews, theme: String) {
        val dark = isDark(context, theme)

        views.setInt(
            R.id.widget_root,
            "setBackgroundResource",
            when {
                theme == "transparent" -> R.drawable.widget_background_transparent
                dark -> R.drawable.widget_background_dark
                else -> R.drawable.widget_background_light
            },
        )

        val ink = context.getColor(if (dark) R.color.widget_ink_dark else R.color.widget_ink_light)
        val muted =
            context.getColor(if (dark) R.color.widget_muted_dark else R.color.widget_muted_light)
        val brand =
            context.getColor(if (dark) R.color.widget_brand_dark else R.color.widget_brand_light)

        views.setTextColor(R.id.widget_title, ink)
        views.setTextColor(R.id.widget_date, muted)
        views.setTextColor(R.id.widget_headline, brand)
        views.setTextColor(R.id.widget_subtitle, muted)
    }

    /**
     * Whether to paint on dark ink.
     *
     * `system` follows the phone, which is what the launcher expects; the other
     * three are the admin overriding it, because a reader may run the app dark
     * and still want a light widget on a light wallpaper — and the launcher
     * gives no way to ask.
     */
    private fun isDark(context: Context, theme: String): Boolean {
        val night = (context.resources.configuration.uiMode and
            android.content.res.Configuration.UI_MODE_NIGHT_MASK) ==
            android.content.res.Configuration.UI_MODE_NIGHT_YES

        return when (theme) {
            "light" -> false
            "dark" -> true
            "transparent" -> night
            else -> night
        }
    }

    /** Tapping anywhere opens the app. */
    private fun launchIntent(context: Context): PendingIntent {
        val intent = context.packageManager.getLaunchIntentForPackage(context.packageName)
            ?: Intent(context, MainActivity::class.java)

        return PendingIntent.getActivity(
            context,
            0,
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
    }
}
