import Flutter
import UIKit
import UserNotifications

@main
@objc class AppDelegate: FlutterAppDelegate, FlutterImplicitEngineDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    // One notification centre delegate for the whole app, and it has to be
    // this one. FlutterAppDelegate forwards `willPresent` and `didReceive` to
    // every plugin that asked, which is how both flutter_local_notifications
    // (a reminder the phone scheduled) and firebase_messaging (a broadcast the
    // server pushed) learn of a tap. Leave it unset and a notification in the
    // foreground is swallowed, and a tap opens the home screen instead of the
    // route it carried.
    UNUserNotificationCenter.current().delegate = self

    // Asks APNs for a device token. It shows no prompt — the permission dialog
    // is `requestPermission()` in push_service.dart — and firebase_messaging
    // would call it too; calling it here just means the token is on its way
    // before Dart has finished starting.
    //
    // The token itself is handed to FCM by firebase_messaging's app-delegate
    // proxy (FirebaseAppDelegateProxyEnabled, on by default), so there is no
    // `didRegisterForRemoteNotificationsWithDeviceToken` here. Don't turn the
    // proxy off without adding one: FCM would get no APNs token, `getToken()`
    // would return null, and push would stop without any error.
    application.registerForRemoteNotifications()

    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  func didInitializeImplicitFlutterEngine(_ engineBridge: FlutterImplicitEngineBridge) {
    GeneratedPluginRegistrant.register(with: engineBridge.pluginRegistry)
  }
}
