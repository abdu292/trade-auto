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
