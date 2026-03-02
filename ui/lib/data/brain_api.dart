import 'package:dio/dio.dart';

import '../domain/models.dart';

class BrainApi {
  const BrainApi(this._dio);

  final Dio _dio;

  Future<bool> getHealth() async {
    final response = await _dio.get('/health');
    return response.statusCode == 200;
  }

  Future<List<StrategyProfile>> getStrategies() async {
    final response = await _dio.get('/api/strategies/');
    return _asList(response.data).map(StrategyProfile.fromJson).toList();
  }

  Future<List<RiskProfile>> getRiskProfiles() async {
    final response = await _dio.get('/api/risk/profiles');
    return _asList(response.data).map(RiskProfile.fromJson).toList();
  }

  Future<List<TradeItem>> getTrades() async {
    final response = await _dio.get('/api/trades/active');
    return _asList(response.data).map(TradeItem.fromJson).toList();
  }

  Future<List<TradeSignal>> getSignals() async {
    final response = await _dio.get('/api/signals/');
    return _asList(response.data).map(TradeSignal.fromJson).toList();
  }

  Future<List<SessionStateItem>> getSessions() async {
    final response = await _dio.get('/api/sessions/');
    return _asList(response.data).map(SessionStateItem.fromJson).toList();
  }

  Future<LedgerState> getLedgerState() async {
    final response = await _dio.get('/api/monitoring/ledger');
    return LedgerState.fromJson(_asMap(response.data));
  }

  Future<List<NotificationFeedItem>> getNotifications({int take = 20}) async {
    final response = await _dio
        .get('/api/monitoring/notifications', queryParameters: {'take': take});
    return _asList(response.data).map(NotificationFeedItem.fromJson).toList();
  }

  Future<List<PendingApproval>> getApprovals({int take = 20}) async {
    final response = await _dio
        .get('/api/monitoring/approvals', queryParameters: {'take': take});
    return _asList(response.data).map(PendingApproval.fromJson).toList();
  }

  Future<RuntimeStatus> getRuntimeStatus() async {
    final response = await _dio.get('/api/monitoring/runtime');
    return RuntimeStatus.fromJson(_asMap(response.data));
  }

  Future<RuntimeSettings> getRuntimeSettings() async {
    final response = await _dio.get('/api/monitoring/runtime-settings');
    return RuntimeSettings.fromJson(_asMap(response.data));
  }

  Future<RuntimeSettings> updateRuntimeSymbol(String symbol) async {
    final response = await _dio.put('/api/monitoring/runtime-settings', data: {
      'symbol': symbol,
    });
    return RuntimeSettings.fromJson(_asMap(response.data));
  }

  Future<AiHealthStatus> getAiHealthStatus() async {
    final response = await _dio.get('/api/monitoring/ai-health');
    return AiHealthStatus.fromJson(_asMap(response.data));
  }

  Future<List<HazardWindow>> getHazardWindows() async {
    final response = await _dio.get('/api/monitoring/hazard-windows');
    return _asList(response.data).map(HazardWindow.fromJson).toList();
  }

  Future<void> createHazardWindow({
    required String title,
    required String category,
    required DateTime startUtc,
    required DateTime endUtc,
  }) async {
    await _dio.post('/api/monitoring/hazard-windows', data: {
      'title': title,
      'category': category,
      'startUtc': startUtc.toUtc().toIso8601String(),
      'endUtc': endUtc.toUtc().toIso8601String(),
      'isBlocked': true,
    });
  }

  Future<void> disableHazardWindow(String id) async {
    await _dio.post('/api/monitoring/hazard-windows/$id/disable');
  }

  Future<TradeSignal> analyzeSnapshot(AnalyzeSnapshotInput input) async {
    final response =
        await _dio.post('/api/signals/analyze', data: input.toJson());
    return TradeSignal.fromJson(_asMap(response.data));
  }

  Future<void> toggleSession(String session, bool isEnabled) async {
    await _dio.put('/api/sessions/toggle',
        data: {'session': session, 'isEnabled': isEnabled});
  }

  Future<void> activateStrategy(String id) async {
    await _dio.put('/api/strategies/$id/activate');
  }

  Future<void> activateRisk(String id) async {
    await _dio.put('/api/risk/profiles/$id/activate');
  }

  Future<void> approveTrade(String tradeId) async {
    await _dio.post('/api/monitoring/approvals/$tradeId/approve');
  }

  Future<void> rejectTrade(String tradeId) async {
    await _dio.post('/api/monitoring/approvals/$tradeId/reject');
  }

  Future<LedgerState> ledgerDeposit(
      {required double amountAed, required String note}) async {
    final response = await _dio.post('/api/monitoring/ledger/deposit',
        data: {'amountAed': amountAed, 'note': note});
    return LedgerState.fromJson(
        _asMap((_asMap(response.data))['ledger'] ?? response.data));
  }

  Future<LedgerState> ledgerWithdraw(
      {required double amountAed, required String note}) async {
    final response = await _dio.post('/api/monitoring/ledger/withdraw',
        data: {'amountAed': amountAed, 'note': note});
    return LedgerState.fromJson(
        _asMap((_asMap(response.data))['ledger'] ?? response.data));
  }

  Future<LedgerState> ledgerAdjustment(
      {required double adjustmentAed, required String note}) async {
    final response = await _dio.post('/api/monitoring/ledger/adjustment',
        data: {'adjustmentAed': adjustmentAed, 'note': note});
    return LedgerState.fromJson(
        _asMap((_asMap(response.data))['ledger'] ?? response.data));
  }
}

Map<String, dynamic> _asMap(dynamic data) {
  if (data is Map<String, dynamic>) {
    return data;
  }

  if (data is Map) {
    return data.map((key, value) => MapEntry(key.toString(), value));
  }

  return const <String, dynamic>{};
}

List<Map<String, dynamic>> _asList(dynamic data) {
  if (data is! List) {
    return const [];
  }

  return data
      .map((item) => item is Map<String, dynamic>
          ? item
          : item is Map
              ? item.map((key, value) => MapEntry(key.toString(), value))
              : const <String, dynamic>{})
      .toList();
}
