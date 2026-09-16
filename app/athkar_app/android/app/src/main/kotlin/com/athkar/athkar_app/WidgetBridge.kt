package com.athkar.athkar_app

import android.content.Context
import io.flutter.plugin.common.MethodCall
import io.flutter.plugin.common.MethodChannel

/**
 * Receives the widget's content from the app and repaints the launcher.
 *
 * The values arrive already formatted — Arabic-Indic numerals, a 12-hour clock,
 * the Hijri date — because all of that is decided by the reader's settings on
 * the Dart side. Re-deciding any of it here would mean two implementations of
 * the same preference.
 */
object WidgetBridge {
    const val CHANNEL = "athkari/widget"

    fun handle(context: Context, call: MethodCall, result: MethodChannel.Result) {
        when (call.method) {
            "update" -> {
                val prefs = context
                    .getSharedPreferences(AthkarWidget.PREFS, Context.MODE_PRIVATE)
                    .edit()

                prefs.putBoolean(AthkarWidget.KEY_ENABLED, call.argument<Boolean>("enabled") ?: true)

                for (key in listOf(
                    AthkarWidget.KEY_WIDGET_KEY,
                    AthkarWidget.KEY_KIND,
                    AthkarWidget.KEY_THEME,
                    AthkarWidget.KEY_DATE,
                    AthkarWidget.KEY_HEADLINE,
                    AthkarWidget.KEY_TITLE,
                    AthkarWidget.KEY_SUBTITLE,
                    AthkarWidget.KEY_PLACEHOLDER,
                )) {
                    prefs.putString(key, call.argument<String>(key) ?: "")
                }

                // Joined rather than stored as a set: the order of the five
                // prayers is the content, and a `StringSet` has none.
                for (key in listOf(
                    AthkarWidget.KEY_PRAYER_LABELS,
                    AthkarWidget.KEY_PRAYER_TIMES,
                )) {
                    val values = call.argument<List<String>>(key) ?: emptyList()
                    prefs.putString(key, values.joinToString(AthkarWidget.SEPARATOR))
                }

                prefs.putInt(
                    AthkarWidget.KEY_PRAYER_HIGHLIGHT,
                    call.argument<Int>(AthkarWidget.KEY_PRAYER_HIGHLIGHT) ?: -1,
                )

                // Committed, not applied: the repaint below reads this file back
                // immediately, and an asynchronous write can lose that race.
                prefs.commit()

                AthkarWidget.refresh(context)
                result.success(true)
            }

            /**
             * Whether any widget is actually on a home screen. The app uses this
             * to tell "not set up yet" from "set up and showing", instead of
             * claiming one or the other.
             */
            "isPlaced" -> {
                val manager = android.appwidget.AppWidgetManager.getInstance(context)
                val ids = manager.getAppWidgetIds(
                    android.content.ComponentName(context, AthkarWidget::class.java)
                )
                result.success(ids.isNotEmpty())
            }

            else -> result.notImplemented()
        }
    }
}
