import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

enum ApiEnvironment { production, local }

final selectedApiEnvironmentProvider = StateProvider<ApiEnvironment>(
  (ref) => ApiEnvironment.production,
);

final productionApiBaseUrlProvider = Provider<String>((ref) {
  return _stripApiSuffix('https://trade-auto.azurewebsites.net');
});

final localApiBaseUrlProvider = Provider<String>((ref) {
  if (kIsWeb) {
    return 'http://localhost:5000';
  }

  switch (defaultTargetPlatform) {
    case TargetPlatform.android:
      return 'http://10.0.2.2:5000';
    default:
      return 'http://localhost:5000';
  }
});

final effectiveApiBaseUrlProvider = Provider<String>((ref) {
  final selected = ref.watch(selectedApiEnvironmentProvider);
  final productionBase = ref.watch(productionApiBaseUrlProvider);
  final localBase = ref.watch(localApiBaseUrlProvider);

  return switch (selected) {
    ApiEnvironment.production => productionBase,
    ApiEnvironment.local => localBase,
  };
});

final dioProvider = Provider<Dio>((ref) {
  final baseUrl = ref.watch(effectiveApiBaseUrlProvider);

  return Dio(
    BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
    ),
  );
});

String _stripApiSuffix(String baseUrl) {
  var normalized = baseUrl.endsWith('/')
      ? baseUrl.substring(0, baseUrl.length - 1)
      : baseUrl;
  if (normalized.endsWith('/api')) {
    normalized = normalized.substring(0, normalized.length - 4);
  }
  return normalized;
}
