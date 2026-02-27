import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key, required this.isEmergencyPaused});

  final bool isEmergencyPaused;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final health = ref.watch(healthProvider);
    final ledger = ref.watch(ledgerProvider);
    final approvals = ref.watch(approvalsProvider);
    final notifications = ref.watch(notificationsProvider);

    Future<void> refresh() async {
      ref
        ..invalidate(healthProvider)
        ..invalidate(ledgerProvider)
        ..invalidate(approvalsProvider)
        ..invalidate(notificationsProvider);
    }

    Future<void> mutateApproval(
        {required String tradeId, required bool approve}) async {
      final messenger = ScaffoldMessenger.of(context);
      final api = ref.read(brainApiProvider);
      try {
        if (approve) {
          await api.approveTrade(tradeId);
        } else {
          await api.rejectTrade(tradeId);
        }
        await refresh();
      } catch (error) {
        messenger
            .showSnackBar(SnackBar(content: Text('Action failed: $error')));
      }
    }

    return RefreshIndicator(
      onRefresh: refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Icon(isEmergencyPaused
                      ? Icons.pause_circle_filled
                      : Icons.play_circle_fill),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      isEmergencyPaused
                          ? 'Emergency pause is ON'
                          : 'Automation is active',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('System Health',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  health.when(
                    data: (ok) => Chip(
                      avatar: Icon(ok ? Icons.check_circle : Icons.error),
                      label: Text(ok ? 'Backend healthy' : 'Backend unhealthy'),
                    ),
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Error: $error'),
                  ),
                  const SizedBox(height: 8),
                  ledger.when(
                    data: (state) => Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        _MetricChip(
                            label: 'Cash (AED)',
                            value: state.cashAed.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Gold (g)',
                            value: state.goldGrams.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Deployable',
                            value: state.deployableCashAed.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Exposure %',
                            value:
                                state.openExposurePercent.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Open Buys',
                            value: state.openBuyCount.toString()),
                      ],
                    ),
                    loading: () => const Padding(
                      padding: EdgeInsets.only(top: 8),
                      child: LinearProgressIndicator(),
                    ),
                    error: (error, _) => Text('Ledger error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Manual Approvals',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  approvals.when(
                    data: (items) {
                      if (items.isEmpty) {
                        return const Text('No pending approvals.');
                      }
                      return Column(
                        children: items
                            .map(
                              (item) => ListTile(
                                dense: true,
                                contentPadding: EdgeInsets.zero,
                                title: Text(
                                    '${item.symbol} • ${item.type} • ${item.grams.toStringAsFixed(2)}g'),
                                subtitle: Text(
                                    'Entry ${item.price.toStringAsFixed(2)} • TP ${item.tp.toStringAsFixed(2)}'),
                                trailing: Wrap(
                                  spacing: 4,
                                  children: [
                                    FilledButton.tonal(
                                      onPressed: () => mutateApproval(
                                          tradeId: item.id, approve: false),
                                      child: const Text('Reject'),
                                    ),
                                    FilledButton(
                                      onPressed: () => mutateApproval(
                                          tradeId: item.id, approve: true),
                                      child: const Text('Approve'),
                                    ),
                                  ],
                                ),
                              ),
                            )
                            .toList(),
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Approvals error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Notifications',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  notifications.when(
                    data: (items) {
                      if (items.isEmpty) {
                        return const Text('No notifications yet.');
                      }
                      return Column(
                        children: items
                            .take(10)
                            .map(
                              (item) => ListTile(
                                dense: true,
                                contentPadding: EdgeInsets.zero,
                                leading: const Icon(Icons.notifications),
                                title: Text(item.title),
                                subtitle:
                                    Text('${item.channel} • ${item.message}'),
                              ),
                            )
                            .toList(),
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Notifications error: $error'),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MetricChip extends StatelessWidget {
  const _MetricChip({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Chip(
      label: Text('$label: $value'),
    );
  }
}
