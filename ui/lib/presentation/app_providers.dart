import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_client.dart';
import '../data/brain_api.dart';
import '../domain/models.dart';

final brainApiProvider = Provider<BrainApi>((ref) {
  return BrainApi(ref.watch(dioProvider));
});

final healthProvider = FutureProvider<bool>((ref) {
  return ref.watch(brainApiProvider).getHealth();
});

final strategiesProvider = FutureProvider<List<StrategyProfile>>((ref) {
  return ref.watch(brainApiProvider).getStrategies();
});

final riskProfilesProvider = FutureProvider<List<RiskProfile>>((ref) {
  return ref.watch(brainApiProvider).getRiskProfiles();
});

final activeTradesProvider = FutureProvider<List<TradeItem>>((ref) {
  return ref.watch(brainApiProvider).getTrades();
});

final signalsProvider = FutureProvider<List<TradeSignal>>((ref) {
  return ref.watch(brainApiProvider).getSignals();
});

final sessionsProvider = FutureProvider<List<SessionStateItem>>((ref) {
  return ref.watch(brainApiProvider).getSessions();
});

final ledgerProvider = FutureProvider<LedgerState>((ref) {
  return ref.watch(brainApiProvider).getLedgerState();
});

final notificationsProvider = FutureProvider<List<NotificationFeedItem>>((ref) {
  return ref.watch(brainApiProvider).getNotifications();
});

final approvalsProvider = FutureProvider<List<PendingApproval>>((ref) {
  return ref.watch(brainApiProvider).getApprovals();
});

final runtimeStatusProvider = FutureProvider<RuntimeStatus>((ref) {
  return ref.watch(brainApiProvider).getRuntimeStatus();
});

final runtimeSettingsProvider = FutureProvider<RuntimeSettings>((ref) {
  return ref.watch(brainApiProvider).getRuntimeSettings();
});

final aiHealthStatusProvider = FutureProvider<AiHealthStatus>((ref) {
  return ref.watch(brainApiProvider).getAiHealthStatus();
});

final hazardWindowsProvider = FutureProvider<List<HazardWindow>>((ref) {
  return ref.watch(brainApiProvider).getHazardWindows();
});

final kpiProvider = FutureProvider<KpiStats>((ref) {
  return ref.watch(brainApiProvider).getKpiStats();
});

// Spec v7 §10 — Gold Engine dashboard (ledger truth + factor states + trade map)
final goldDashboardProvider = FutureProvider<GoldDashboard>((ref) {
  return ref.watch(brainApiProvider).getGoldEngineDashboard();
});

final chartDataProvider = FutureProvider<ChartData>((ref) {
  return ref.watch(brainApiProvider).getChartData();
});

final replayStatusProvider = FutureProvider<ReplayStatusResponse>((ref) {
  return ref.watch(brainApiProvider).getReplayStatus();
});

// UI-only flag for emergency pause.  Not persisted or sent to backend.
final emergencyPauseProvider = StateProvider<bool>((ref) => false);

final timelineProvider = FutureProvider<List<RuntimeTimelineItem>>((ref) {
  return ref.watch(brainApiProvider).getTimelineEvents(take: 200);
});
