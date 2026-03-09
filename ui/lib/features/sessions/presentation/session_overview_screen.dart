import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class SessionOverviewScreen extends ConsumerWidget {
  const SessionOverviewScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessions = ref.watch(sessionsProvider);
    final kpi = ref.watch(kpiProvider);

    Future<void> toggle(String session, bool isEnabled) async {
      final messenger = ScaffoldMessenger.of(context);
      try {
        await ref.read(brainApiProvider).toggleSession(session, isEnabled);
        ref.invalidate(sessionsProvider);
      } catch (error) {
        messenger.showSnackBar(
            SnackBar(content: Text('Failed to update session: $error')));
      }
    }

    return RefreshIndicator(
      onRefresh: () async {
        ref
          ..invalidate(sessionsProvider)
          ..invalidate(kpiProvider);
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Session Controls',
              style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          sessions.when(
            data: (items) {
              if (items.isEmpty) {
                return const Card(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No session records available.'),
                  ),
                );
              }
              return Column(
                children: items
                    .map(
                      (item) => Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Card(
                          child: SwitchListTile(
                            value: item.isEnabled,
                            title: Text(item.session),
                            subtitle:
                                Text('Updated: ${item.updatedAtUtc.toLocal()}'),
                            onChanged: (value) => toggle(item.session, value),
                          ),
                        ),
                      ),
                    )
                    .toList(),
              );
            },
            loading: () => const Padding(
              padding: EdgeInsets.all(16),
              child: LinearProgressIndicator(),
            ),
            error: (error, _) => Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Text('Error loading sessions: $error'),
              ),
            ),
          ),
          const SizedBox(height: 16),
          // KPI Card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Performance KPIs',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  kpi.when(
                    data: (stats) => Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Today (${stats.todayKsaDate})',
                            style: Theme.of(context).textTheme.titleSmall),
                        const SizedBox(height: 6),
                        Wrap(
                          spacing: 8,
                          runSpacing: 6,
                          children: [
                            Chip(
                                visualDensity: VisualDensity.compact,
                                label: Text(
                                    'Profit: AED ${stats.todayProfitAed.toStringAsFixed(2)}')),
                            Chip(
                                visualDensity: VisualDensity.compact,
                                label: Text(
                                    'Rotations: ${stats.todayRotations}')),
                            Chip(
                                visualDensity: VisualDensity.compact,
                                label: Text(
                                    'Avg: AED ${stats.todayAvgProfitAed.toStringAsFixed(2)}')),
                            Chip(
                                visualDensity: VisualDensity.compact,
                                label: Text(
                                    'Hit Rate: ${(stats.todayHitRate * 100).toStringAsFixed(1)}%')),
                          ],
                        ),
                        const SizedBox(height: 10),
                        Text('Per-Session Stats',
                            style: Theme.of(context).textTheme.titleSmall),
                        const SizedBox(height: 6),
                        if (stats.sessionStats.isEmpty)
                          const Text('No per-session data available.')
                        else
                          Table(
                            border: TableBorder.all(
                                color: Theme.of(context)
                                    .colorScheme
                                    .outlineVariant),
                            columnWidths: const {
                              0: FlexColumnWidth(2),
                              1: FlexColumnWidth(2),
                              2: FlexColumnWidth(1),
                              3: FlexColumnWidth(2),
                            },
                            children: [
                              TableRow(
                                decoration: BoxDecoration(
                                    color: Theme.of(context)
                                        .colorScheme
                                        .surfaceContainerHighest),
                                children: const [
                                  _TCell('Session', header: true),
                                  _TCell('Profit (AED)', header: true),
                                  _TCell('Rot.', header: true),
                                  _TCell('WF Blocks', header: true),
                                ],
                              ),
                              ...stats.sessionStats.entries.map(
                                (e) => TableRow(children: [
                                  _TCell(e.key),
                                  _TCell(
                                      e.value.profitAed.toStringAsFixed(2)),
                                  _TCell(e.value.rotations.toString()),
                                  _TCell(e.value.waterfallBlocks.toString()),
                                ]),
                              ),
                            ],
                          ),
                        const SizedBox(height: 10),
                        Text('Weekly',
                            style: Theme.of(context).textTheme.titleSmall),
                        const SizedBox(height: 6),
                        Wrap(
                          spacing: 8,
                          runSpacing: 6,
                          children: [
                            Chip(
                                visualDensity: VisualDensity.compact,
                                label: Text(
                                    'Profit: AED ${stats.weeklyProfitAed.toStringAsFixed(2)}')),
                            Chip(
                                visualDensity: VisualDensity.compact,
                                label: Text(
                                    'Rotations: ${stats.weeklyRotations}')),
                            if (stats.weeklyBestSession.isNotEmpty)
                              Chip(
                                  visualDensity: VisualDensity.compact,
                                  label: Text(
                                      '🏆 Best: ${stats.weeklyBestSession}')),
                            if (stats.weeklyWorstSession.isNotEmpty)
                              Chip(
                                  visualDensity: VisualDensity.compact,
                                  label: Text(
                                      '📉 Worst: ${stats.weeklyWorstSession}')),
                          ],
                        ),
                        if (stats.weeklyNoTradeBlocks.isNotEmpty) ...[
                          const SizedBox(height: 10),
                          Text('NO-TRADE Blocks This Week',
                              style:
                                  Theme.of(context).textTheme.titleSmall),
                          const SizedBox(height: 6),
                          Wrap(
                            spacing: 8,
                            runSpacing: 6,
                            children: stats.weeklyNoTradeBlocks.entries
                                .map((e) => Chip(
                                      visualDensity: VisualDensity.compact,
                                      label: Text('${e.key}: ${e.value}'),
                                    ))
                                .toList(),
                          ),
                        ],
                      ],
                    ),
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('KPI error: $error'),
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

class _TCell extends StatelessWidget {
  const _TCell(this.text, {this.header = false});

  final String text;
  final bool header;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      child: Text(
        text,
        style: header
            ? const TextStyle(fontWeight: FontWeight.bold)
            : null,
      ),
    );
  }
}

