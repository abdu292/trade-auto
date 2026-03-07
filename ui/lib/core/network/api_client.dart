import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

enum ApiEnvironment { production, local }

const _apiEnvironmentPrefKey = 'selected_api_environment';

ApiEnvironment _parseApiEnvironment(String? raw) {
  for (final env in ApiEnvironment.values) {
    if (env.name == raw) {
      return env;
    }
  }
  return ApiEnvironment.production;
}

class SelectedApiEnvironmentNotifier extends Notifier<ApiEnvironment> {
  @override
  ApiEnvironment build() {
    _hydrateFromStorage();
    return ApiEnvironment.production;
  }

  Future<void> setEnvironment(ApiEnvironment environment) async {
    if (state == environment) {
      return;
    }

    state = environment;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_apiEnvironmentPrefKey, environment.name);
  }

  Future<void> _hydrateFromStorage() async {
    final prefs = await SharedPreferences.getInstance();
    final restored = _parseApiEnvironment(prefs.getString(_apiEnvironmentPrefKey));

    if (state != restored) {
      state = restored;
    }
  }
}

final selectedApiEnvironmentProvider =
    NotifierProvider<SelectedApiEnvironmentNotifier, ApiEnvironment>(
      SelectedApiEnvironmentNotifier.new,
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
