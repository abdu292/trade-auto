import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

final dioProvider = Provider<Dio>((ref) {
  const configuredBase =
      String.fromEnvironment('BRAIN_API_BASE_URL', defaultValue: '');
  final baseUrl = _resolveBaseUrl(configuredBase);

  return Dio(
    BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
    ),
  );
});

String _resolveBaseUrl(String configuredBase) {
  if (configuredBase.trim().isNotEmpty) {
    return _stripApiSuffix(configuredBase.trim());
  }

  if (kIsWeb) {
    return 'http://localhost:5000';
  }

  switch (defaultTargetPlatform) {
    case TargetPlatform.android:
      return 'http://10.0.2.2:5000';
    default:
      return 'http://localhost:5000';
  }
}

String _stripApiSuffix(String baseUrl) {
  var normalized = baseUrl.endsWith('/')
      ? baseUrl.substring(0, baseUrl.length - 1)
      : baseUrl;
  if (normalized.endsWith('/api')) {
    normalized = normalized.substring(0, normalized.length - 4);
  }
  return normalized;
}
