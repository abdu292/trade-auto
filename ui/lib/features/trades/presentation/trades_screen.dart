import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class TradesScreen extends ConsumerStatefulWidget {
  const TradesScreen({super.key});

  @override
  ConsumerState<TradesScreen> createState() => _TradesScreenState();
}

class _TradesScreenState extends ConsumerState<TradesScreen> {
  @override
  Widget build(BuildContext context) {
    final trades = ref.watch(activeTradesProvider);
    final signals = ref.watch(signalsProvider);
    final runtime = ref.watch(runtimeStatusProvider);

    Future<void> refresh() async {
      ref
        ..invalidate(activeTradesProvider)
        ..invalidate(signalsProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(notificationsProvider);
    }

    return RefreshIndicator(
      onRefresh: refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _SectionCard(
            title: 'Live Runtime',
            child: runtime.when(
              data: (state) => Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Symbol ${state.symbol} • Session ${state.session}'),
                  const SizedBox(height: 4),
                  Text(
                      'Bid ${state.bid.toStringAsFixed(2)} • Ask ${state.ask.toStringAsFixed(2)} • Spread ${state.spread.toStringAsFixed(3)}'),
                  const SizedBox(height: 4),
                  Text(
                      'Queue ${state.pendingQueueDepth} • Telegram ${state.telegramState} • TV ${state.tvAlertType}'),
                    const SizedBox(height: 4),
                    Text(
                      'Macro ${state.macroBias}/${state.institutionalBias} • Hazard ${state.activeBlockedHazardWindows} • CacheAge ${state.macroCacheAgeMinutes}m'),
                ],
              ),
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text('Runtime error: $error'),
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'Active Trades',
            child: trades.when(
              data: (items) {
                if (items.isEmpty) {
                  return const Text('No active trades.');
                }
                return Column(
                  children: items
                      .map(
                        (item) => ListTile(
                          dense: true,
                          contentPadding: EdgeInsets.zero,
                          title: Text('${item.symbol} • ${item.rail}'),
                          subtitle: Text(
                            'Entry ${item.entry.toStringAsFixed(2)} • TP ${item.tp.toStringAsFixed(2)} • ${item.status}',
                          ),
                        ),
                      )
                      .toList(),
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text('Trades error: $error'),
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'Recent Signals',
            child: signals.when(
              data: (items) {
                if (items.isEmpty) {
                  return const Text('No signals generated yet.');
                }
                return Column(
                  children: items
                      .take(10)
                      .map(
                        (signal) => ListTile(
                          dense: true,
                          contentPadding: EdgeInsets.zero,
                          title: Text('${signal.symbol} • ${signal.rail}'),
                          subtitle: Text(
                            'Entry ${signal.entry.toStringAsFixed(2)} • TP ${signal.tp.toStringAsFixed(2)} • Confidence ${(signal.confidence * 100).toStringAsFixed(1)}%',
                          ),
                        ),
                      )
                      .toList(),
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text('Signals error: $error'),
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            child,
          ],
        ),
      ),
    );
  }
}
