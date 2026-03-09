import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class TradesScreen extends ConsumerStatefulWidget {
  const TradesScreen({super.key});

  @override
  ConsumerState<TradesScreen> createState() => _TradesScreenState();
}

class _TradesScreenState extends ConsumerState<TradesScreen> {
  final Set<String> _busyApprovals = <String>{};

  Future<void> _approveTrade(String tradeId) async {
    if (_busyApprovals.contains(tradeId)) {
      return;
    }

    setState(() => _busyApprovals.add(tradeId));
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(brainApiProvider).approveTrade(tradeId);
      ref
        ..invalidate(approvalsProvider)
        ..invalidate(activeTradesProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(notificationsProvider);
      messenger.showSnackBar(
        const SnackBar(content: Text('Trade placed to MT5 queue.')),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to place trade: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _busyApprovals.remove(tradeId));
      }
    }
  }

  Future<void> _rejectTrade(String tradeId) async {
    if (_busyApprovals.contains(tradeId)) {
      return;
    }

    setState(() => _busyApprovals.add(tradeId));
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(brainApiProvider).rejectTrade(tradeId);
      ref
        ..invalidate(approvalsProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(notificationsProvider);
      messenger.showSnackBar(
        const SnackBar(content: Text('Trade rejected.')),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to reject trade: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _busyApprovals.remove(tradeId));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final trades = ref.watch(activeTradesProvider);
    final signals = ref.watch(signalsProvider);
    final runtime = ref.watch(runtimeStatusProvider);
    final approvals = ref.watch(approvalsProvider);

    Future<void> refresh() async {
      ref
        ..invalidate(activeTradesProvider)
        ..invalidate(signalsProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(approvalsProvider)
        ..invalidate(notificationsProvider);
    }

    return RefreshIndicator(
      onRefresh: refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _SectionCard(
            title: 'Pending Trade Approvals',
            subtitle:
                'Review each suggestion. Do nothing to leave pending, or press Place to execute. This section is only used when Auto Trade is OFF and will stay empty when Auto Trade is ON.',
            child: approvals.when(
              data: (items) {
                if (items.isEmpty) {
                  return const Text('No pending approvals.');
                }
                return Column(
                  children: items
                      .map(
                        (item) => Card(
                          margin: const EdgeInsets.only(bottom: 10),
                          child: Padding(
                            padding: const EdgeInsets.all(14),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    Expanded(
                                      child: Text(
                                        '${item.symbol} • ${item.type}',
                                        style: Theme.of(context)
                                            .textTheme
                                            .titleSmall,
                                      ),
                                    ),
                                    Chip(label: Text(item.riskTag)),
                                  ],
                                ),
                                const SizedBox(height: 8),
                                Wrap(
                                  spacing: 8,
                                  runSpacing: 8,
                                  children: [
                                    _RuntimeChip(
                                        label: 'Entry',
                                        value: item.price.toStringAsFixed(2)),
                                    _RuntimeChip(
                                        label: 'TP',
                                        value: item.tp.toStringAsFixed(2)),
                                    _RuntimeChip(
                                        label: 'Grams',
                                        value: item.grams.toStringAsFixed(2)),
                                    _RuntimeChip(
                                        label: 'Max Life',
                                        value: '${item.ml}s'),
                                    _RuntimeChip(
                                      label: 'Confidence',
                                      value:
                                          '${(item.alignmentScore * 100).toStringAsFixed(1)}%',
                                    ),
                                    _RuntimeChip(
                                      label: 'Regime',
                                      value: item.regime,
                                    ),
                                    _RuntimeChip(
                                      label: 'Consensus',
                                      value:
                                          '${item.agreementCount}/${item.requiredAgreement}',
                                    ),
                                    _RuntimeChip(
                                      label: 'Mode Hint',
                                      value: item.modeHint,
                                    ),
                                    _RuntimeChip(
                                      label: 'Mode Conf',
                                      value:
                                          '${(item.modeConfidence * 100).toStringAsFixed(1)}%',
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 8),
                                Text(
                                  'Expires ${item.expiry.toLocal()}',
                                  style: Theme.of(context).textTheme.bodySmall,
                                ),
                                if (item.summary.isNotEmpty) ...[
                                  const SizedBox(height: 8),
                                  Text(
                                    'AI Summary: ${item.summary}',
                                    style:
                                        Theme.of(context).textTheme.bodySmall,
                                  ),
                                ],
                                if (item.disagreementReason != null &&
                                    item.disagreementReason!.isNotEmpty) ...[
                                  const SizedBox(height: 6),
                                  Text(
                                    'Disagreement: ${item.disagreementReason}',
                                    style: Theme.of(context)
                                        .textTheme
                                        .bodySmall
                                        ?.copyWith(
                                          color: Theme.of(context)
                                              .colorScheme
                                              .tertiary,
                                        ),
                                  ),
                                ],
                                if (item.providerVotes.isNotEmpty) ...[
                                  const SizedBox(height: 8),
                                  Text(
                                    'Provider Votes',
                                    style:
                                        Theme.of(context).textTheme.labelLarge,
                                  ),
                                  const SizedBox(height: 4),
                                  ...item.providerVotes.map(
                                    (vote) => Padding(
                                      padding:
                                          const EdgeInsets.only(bottom: 2.0),
                                      child: Text(
                                        '- $vote',
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodySmall,
                                      ),
                                    ),
                                  ),
                                ],
                                const SizedBox(height: 10),
                                Row(
                                  children: [
                                    FilledButton.icon(
                                      onPressed: _busyApprovals.contains(item.id)
                                          ? null
                                          : () => _approveTrade(item.id),
                                      icon: _busyApprovals.contains(item.id)
                                          ? const SizedBox(
                                              width: 14,
                                              height: 14,
                                              child:
                                                  CircularProgressIndicator(
                                                      strokeWidth: 2),
                                            )
                                          : const Icon(Icons.send),
                                      label: const Text('Place'),
                                    ),
                                    const SizedBox(width: 8),
                                    OutlinedButton.icon(
                                      onPressed: _busyApprovals.contains(item.id)
                                          ? null
                                          : () => _rejectTrade(item.id),
                                      icon: const Icon(Icons.close),
                                      label: const Text('Reject'),
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        ),
                      )
                      .toList(),
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text('Approvals error: $error'),
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'Live Runtime',
            child: runtime.when(
              data: (state) => Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _RuntimeChip(label: 'Symbol', value: state.symbol),
                      _RuntimeChip(label: 'Session', value: state.session),
                      _RuntimeChip(
                        label: 'Execution',
                        value: state.executionMode.toUpperCase(),
                      ),
                      _RuntimeChip(
                        label: 'Auto Sessions',
                        value: state.hybridAutoSessions,
                      ),
                      _RuntimeChip(
                        label: 'Pending Queue',
                        value: state.pendingQueueDepth.toString(),
                      ),
                      _RuntimeChip(
                        label: 'Approval Queue',
                        value: state.approvalQueueDepth.toString(),
                      ),
                      _RuntimeChip(
                        label: 'Bid',
                        value: state.bid.toStringAsFixed(2),
                      ),
                      _RuntimeChip(
                        label: 'Ask',
                        value: state.ask.toStringAsFixed(2),
                      ),
                      _RuntimeChip(
                        label: 'Spread',
                        value: state.spread.toStringAsFixed(3),
                      ),
                      _RuntimeChip(label: 'Telegram', value: state.telegramState),
                      _RuntimeChip(label: 'TV', value: state.tvAlertType),
                      _RuntimeChip(
                        label: 'Macro',
                        value: '${state.macroBias}/${state.institutionalBias}',
                      ),
                      _RuntimeChip(
                        label: 'Hazards',
                        value: state.activeBlockedHazardWindows.toString(),
                      ),
                      _RuntimeChip(
                        label: 'Cache Age',
                        value: '${state.macroCacheAgeMinutes}m',
                      ),
                    ],
                  ),
                ],
              ),
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text('Runtime error: $error'),
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'MT5 Open Positions',
            child: runtime.when(
              data: (state) {
                if (state.openPositions.isEmpty) {
                  return const Text('No open positions on MT5.');
                }
                return Column(
                  children: state.openPositions.map((pos) {
                    final pnlColor = pos.currentPnlPoints >= 0
                        ? Theme.of(context).colorScheme.primary
                        : Theme.of(context).colorScheme.error;
                    return ListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      title: Text(
                        'Entry ${pos.entryPrice.toStringAsFixed(2)} → TP ${pos.tp.toStringAsFixed(2)}',
                      ),
                      subtitle: Text(
                        'P&L ${pos.currentPnlPoints >= 0 ? "+" : ""}${pos.currentPnlPoints.toStringAsFixed(2)} pts • ${pos.volumeGramsEquivalent.toStringAsFixed(2)}g',
                      ),
                      trailing: Text(
                        '${pos.currentPnlPoints >= 0 ? "+" : ""}${pos.currentPnlPoints.toStringAsFixed(2)}',
                        style: TextStyle(
                            color: pnlColor, fontWeight: FontWeight.bold),
                      ),
                    );
                  }).toList(),
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text('Runtime error: $error'),
            ),
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: 'MT5 Pending Orders',
            child: runtime.when(
              data: (state) {
                if (state.pendingOrders.isEmpty) {
                  return const Text('No pending orders on MT5.');
                }
                return Column(
                  children: state.pendingOrders.map((order) {
                    String expiryText;
                    if (order.expiry != null) {
                      final local = order.expiry!.toLocal();
                      final y = local.year;
                      final mo = local.month.toString().padLeft(2, '0');
                      final d = local.day.toString().padLeft(2, '0');
                      final h = local.hour.toString().padLeft(2, '0');
                      final mi = local.minute.toString().padLeft(2, '0');
                      expiryText = 'Exp $y-$mo-$d $h:$mi';
                    } else {
                      expiryText = 'No expiry';
                    }
                    return ListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      title: Text(
                        '${order.type} @ ${order.price.toStringAsFixed(2)} → TP ${order.tp.toStringAsFixed(2)}',
                      ),
                      subtitle: Text(
                        '${order.volumeGramsEquivalent.toStringAsFixed(2)}g • $expiryText',
                      ),
                    );
                  }).toList(),
                );
              },
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
  const _SectionCard({
    required this.title,
    required this.child,
    this.subtitle,
  });

  final String title;
  final Widget child;
  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(18),
        side: BorderSide(color: colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            if (subtitle != null) ...[
              const SizedBox(height: 4),
              Text(subtitle!, style: Theme.of(context).textTheme.bodySmall),
            ],
            const SizedBox(height: 8),
            child,
          ],
        ),
      ),
    );
  }
}

class _RuntimeChip extends StatelessWidget {
  const _RuntimeChip({required this.label, required this.value});

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
