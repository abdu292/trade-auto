import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key, required this.isEmergencyPaused});

  final bool isEmergencyPaused;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colorScheme = Theme.of(context).colorScheme;
    final health = ref.watch(healthProvider);
    final ledger = ref.watch(ledgerProvider);
    final runtime = ref.watch(runtimeStatusProvider);
    final aiHealth = ref.watch(aiHealthStatusProvider);
    final notifications = ref.watch(notificationsProvider);

    Future<void> refresh() async {
      ref
        ..invalidate(healthProvider)
        ..invalidate(ledgerProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(aiHealthStatusProvider)
        ..invalidate(notificationsProvider);
    }

    return RefreshIndicator(
      onRefresh: refresh,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 18,
                    backgroundColor: isEmergencyPaused
                        ? colorScheme.errorContainer
                        : colorScheme.primaryContainer,
                    child: Icon(
                      isEmergencyPaused
                          ? Icons.pause_circle_filled
                          : Icons.play_circle_fill,
                      color: isEmergencyPaused
                          ? colorScheme.error
                          : colorScheme.primary,
                    ),
                  ),
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
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Hard-coded Sessions',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  Text(
                    'Tokyo: 03:00-09:00 KSA | 04:00-10:00 UAE | 05:30-11:30 IST',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  Text(
                    'India: 06:30-14:30 KSA | 07:30-15:30 UAE | 09:00-17:00 IST',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  Text(
                    'London: 10:00-18:00 KSA | 11:00-19:00 UAE | 12:30-20:30 IST',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  Text(
                    'New York: 15:30-23:30 KSA | 16:30-00:30 UAE | 18:00-02:00 IST',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Phases: START (first 20%), MID (middle), END (last 20%).',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('AI Providers',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  aiHealth.when(
                    data: (status) {
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: [
                              _MetricChip(
                                  label: 'Analyzers',
                                  value: status.analyzerCount.toString()),
                              _MetricChip(
                                  label: 'All 4 Active',
                                  value: status.coverage.allFourEnabled
                                      ? 'YES'
                                      : 'NO'),
                              _MetricChip(
                                  label: 'OpenAI',
                                  value: status.coverage.openai ? 'ON' : 'OFF'),
                              _MetricChip(
                                  label: 'Gemini',
                                  value: status.coverage.gemini ? 'ON' : 'OFF'),
                              _MetricChip(
                                  label: 'Grok',
                                  value: status.coverage.grok ? 'ON' : 'OFF'),
                              _MetricChip(
                                  label: 'Perplexity',
                                  value: status.coverage.perplexity
                                      ? 'ON'
                                      : 'OFF'),
                            ],
                          ),
                          if (status.analyzers.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(
                                'Active analyzers: ${status.analyzers.join(', ')}'),
                          ],
                          if (status.parityBlockers.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(
                              'Parity blockers: ${status.parityBlockers.join(' | ')}',
                              style: Theme.of(context)
                                  .textTheme
                                  .bodySmall
                                  ?.copyWith(color: colorScheme.error),
                            ),
                          ],
                        ],
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('AI health error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          _AnimatedCard(
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
                      backgroundColor: ok
                          ? colorScheme.primaryContainer
                          : colorScheme.errorContainer,
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
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Runtime (MT5 Demo/Live)',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  runtime.when(
                    data: (state) {
                      return Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          _MetricChip(label: 'Symbol', value: state.symbol),
                          _MetricChip(label: 'Session', value: state.session),
                          _MetricChip(
                              label: 'Bid',
                              value: state.bid.toStringAsFixed(2)),
                          _MetricChip(
                              label: 'Ask',
                              value: state.ask.toStringAsFixed(2)),
                          _MetricChip(
                              label: 'Spread',
                              value: state.spread.toStringAsFixed(3)),
                          _MetricChip(
                              label: 'Queue Depth',
                              value: state.pendingQueueDepth.toString()),
                          _MetricChip(
                              label: 'Approval Queue',
                              value: state.approvalQueueDepth.toString()),
                          _MetricChip(
                              label: 'Execution Mode',
                              value: state.executionMode.toUpperCase()),
                          _MetricChip(
                              label: 'Hybrid Auto',
                              value: state.hybridAutoSessions),
                          _MetricChip(
                              label: 'Telegram', value: state.telegramState),
                          _MetricChip(
                              label: 'Panic',
                              value: state.panicSuspected ? 'YES' : 'NO'),
                          _MetricChip(
                              label: 'TV Alert', value: state.tvAlertType),
                          _MetricChip(
                              label: 'Macro Bias', value: state.macroBias),
                          _MetricChip(
                              label: 'Institutional',
                              value: state.institutionalBias),
                          _MetricChip(
                              label: 'Hazard Active',
                              value:
                                  state.activeBlockedHazardWindows.toString()),
                          _MetricChip(
                              label: 'Macro Age (m)',
                              value: state.macroCacheAgeMinutes.toString()),
                        ],
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Runtime error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          _AnimatedCard(
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
                                leading: Container(
                                  width: 28,
                                  height: 28,
                                  decoration: BoxDecoration(
                                    color: colorScheme.secondaryContainer,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child:
                                      const Icon(Icons.notifications, size: 18),
                                ),
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
      visualDensity: VisualDensity.compact,
      label: Text('$label: $value'),
    );
  }
}

class _AnimatedCard extends StatelessWidget {
  const _AnimatedCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      child: Card(child: child),
    );
  }
}
