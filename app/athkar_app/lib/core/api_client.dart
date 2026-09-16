import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import 'app_response.dart';
import 'environment.dart';
import 'error_messages.dart';

/// The single gateway to the API.
///
/// It never throws. Transport failures, timeouts and malformed bodies all come
/// back as a failed [AppResponse] carrying a sentence the screen can show, so
/// there is no `try` anywhere above this file and no screen has to know what a
/// `SocketException` is.
///
/// There is no authentication here, and that is not an omission: the app's
/// reader is anonymous. Every call carries `X-Device-Key`, which says *which
/// install* is asking and nothing about who is holding it.
class ApiClient {
  ApiClient._();

  static final instance = ApiClient._();

  final _client = http.Client();

  /// Set once at startup from the settings store.
  String deviceKey = '';

  /// The language the server should resolve content into.
  String languageCode = 'ar';

  /// Strings for the current language, so a failure can be described in it.
  Map<String, String> strings = const {};

  Map<String, String> get _headers => {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        if (deviceKey.isNotEmpty) 'X-Device-Key': deviceKey,
        'Accept-Language': languageCode,
      };

  Uri _uri(String path, [Map<String, dynamic>? query]) {
    final base = Uri.parse(Environment.apiBaseUrl);
    return base.replace(
      path: '${base.path}$path',
      queryParameters: query?.map((key, value) => MapEntry(key, '$value'))
        ?..removeWhere((_, value) => value == 'null'),
    );
  }

  Future<AppResponse<T>> get<T>(
    String path, {
    Map<String, dynamic>? query,
    T Function(dynamic json)? parse,
  }) =>
      _send(() => _client.get(_uri(path, query), headers: _headers), parse);

  Future<AppResponse<T>> post<T>(
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    T Function(dynamic json)? parse,
  }) =>
      _send(
        () => _client.post(
          _uri(path, query),
          headers: _headers,
          body: body == null ? null : jsonEncode(body),
        ),
        parse,
      );

  Future<AppResponse<T>> delete<T>(
    String path, {
    T Function(dynamic json)? parse,
  }) =>
      _send(() => _client.delete(_uri(path), headers: _headers), parse);

  /// Streams a large download, reporting progress.
  ///
  /// Separate from [get] because the Qur'an package is hundreds of megabytes
  /// and must never be held in memory as a string — this hands the caller the
  /// byte stream and the declared length, and the caller writes it to disk.
  Future<AppResponse<http.StreamedResponse>> download(
    String path, {
    Map<String, dynamic>? query,
  }) async {
    try {
      final request = http.Request('GET', _uri(path, query))..headers.addAll(_headers);
      final response = await _client.send(request).timeout(Environment.timeout);

      return response.statusCode == 200
          ? AppResponse.ok(response)
          : _failure(AppErrorCodes.network);
    } on SocketException {
      return _failure(AppErrorCodes.network);
    } on http.ClientException {
      return _failure(AppErrorCodes.network);
    } catch (_) {
      return _failure(AppErrorCodes.unknown);
    }
  }

  /// Runs one call and folds every possible outcome into an [AppResponse].
  Future<AppResponse<T>> _send<T>(
    Future<http.Response> Function() call,
    T Function(dynamic json)? parse,
  ) async {
    http.Response response;

    try {
      response = await call().timeout(Environment.timeout);
    } on SocketException {
      return _failure(AppErrorCodes.network);
    } on http.ClientException {
      return _failure(AppErrorCodes.network);
    } catch (error) {
      // TimeoutException among others. Distinguished from a hard network
      // failure because they call for different sentences: "no connection"
      // versus "the server is slow".
      return _failure(
        error.runtimeType.toString().contains('Timeout')
            ? AppErrorCodes.timeout
            : AppErrorCodes.unknown,
      );
    }

    // A 401 or 403 reaching an anonymous client means an endpoint was called
    // that should never have been. Reported rather than retried.
    if (response.statusCode >= 500) return _failure(AppErrorCodes.unknown);

    try {
      final envelope = jsonDecode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
      final success = envelope['success'] as bool? ?? false;

      if (!success) {
        final code = envelope['errorCode'] as int? ?? AppErrorCodes.unknown;
        return _failure(code);
      }

      final data = envelope['data'];
      return AppResponse.ok(parse == null ? data as T? : (data == null ? null : parse(data)));
    } catch (error) {
      assert(() {
        debugPrint('[api] could not parse a response: $error');
        return true;
      }());
      return _failure(AppErrorCodes.parse);
    }
  }

  AppResponse<T> _failure<T>(int code) =>
      AppResponse<T>.failure(code, ErrorMessages.resolve(code, languageCode, strings));
}
