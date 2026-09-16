/// The result of one API call, as the screens see it.
///
/// There is no exception anywhere above [ApiClient]: transport failures,
/// timeouts, malformed JSON and server-side failures all arrive here, so a
/// screen only ever checks [success] and reads either [data] or [errorMessage].
class AppResponse<T> {
  const AppResponse({
    required this.success,
    this.data,
    this.errorCode = 0,
    this.errorMessage,
  });

  final bool success;
  final T? data;

  /// The server's typed code, or one of the local ones in [AppErrorCodes].
  final int errorCode;

  /// Already localised, ready to show. See `error_messages.dart`.
  final String? errorMessage;

  factory AppResponse.ok(T? data) => AppResponse(success: true, data: data);

  factory AppResponse.failure(int code, String message) =>
      AppResponse(success: false, errorCode: code, errorMessage: message);

  /// Re-shapes a failure for a different payload type, so a caller can forward
  /// one without inventing a message of its own.
  AppResponse<R> cast<R>() =>
      AppResponse<R>(success: false, errorCode: errorCode, errorMessage: errorMessage);
}

/// Codes the client raises itself. Numbered above the server's range so the two
/// sets can share one `switch` without ever colliding.
class AppErrorCodes {
  const AppErrorCodes._();

  static const network = 9000;
  static const timeout = 9001;
  static const parse = 9002;
  static const unknown = 9003;

  /// Nothing cached and nothing reachable — the only state where the app has
  /// genuinely nothing to show, and worth its own sentence to the reader.
  static const offlineNoCache = 9004;
}
