package com.athkar.athkar_app

import android.hardware.GeomagneticField
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

/**
 * Hosts one method channel, for the one thing Flutter cannot get from a
 * package: the magnetic declination.
 *
 * Android's [GeomagneticField] is the World Magnetic Model, kept current by the
 * OS. The alternative was to carry a copy of the model's coefficients in the
 * app, where it would go stale five years after release without anybody
 * noticing. See `lib/core/true_heading.dart` for the whole reasoning.
 *
 * iOS needs no equivalent: `CLHeading.trueHeading` is already corrected.
 */
class MainActivity : FlutterActivity() {
    private companion object {
        const val GEOMAGNETIC_CHANNEL = "athkari/geomagnetic"
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, GEOMAGNETIC_CHANNEL)
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "declination" -> {
                        val latitude = call.argument<Double>("latitude")
                        val longitude = call.argument<Double>("longitude")

                        if (latitude == null || longitude == null) {
                            result.error("missing-coordinates", "latitude and longitude are required", null)
                            return@setMethodCallHandler
                        }

                        // Altitude is passed as zero: the declination changes by
                        // far less than a tenth of a degree over any height a
                        // person stands at, and asking the reader for it would
                        // be absurd.
                        val field = GeomagneticField(
                            latitude.toFloat(),
                            longitude.toFloat(),
                            0f,
                            System.currentTimeMillis(),
                        )

                        result.success(field.declination.toDouble())
                    }

                    else -> result.notImplemented()
                }
            }

        // The home-screen widget's content. See WidgetBridge for why the values
        // are written here rather than read out of the Flutter plugin's store.
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, WidgetBridge.CHANNEL)
            .setMethodCallHandler { call, result ->
                WidgetBridge.handle(applicationContext, call, result)
            }
    }
}
