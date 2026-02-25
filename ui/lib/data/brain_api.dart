import 'package:dio/dio.dart';

class BrainApi {
  const BrainApi(this._dio);

  final Dio _dio;

  Future<List<dynamic>> getStrategies() async {
    final response = await _dio.get('/strategies');
    return response.data as List<dynamic>;
  }

  Future<List<dynamic>> getRiskProfiles() async {
    final response = await _dio.get('/risk/profiles');
    return response.data as List<dynamic>;
  }

  Future<List<dynamic>> getTrades() async {
    final response = await _dio.get('/trades/active');
    return response.data as List<dynamic>;
  }

  Future<List<dynamic>> getSessions() async {
    final response = await _dio.get('/sessions');
    return response.data as List<dynamic>;
  }

  Future<void> toggleSession(String session, bool isEnabled) async {
    await _dio.put('/sessions/toggle', data: {'session': session, 'isEnabled': isEnabled});
  }

  Future<void> activateStrategy(String id) async {
    await _dio.put('/strategies/$id/activate');
  }

  Future<void> activateRisk(String id) async {
    await _dio.put('/risk/profiles/$id/activate');
  }
}
