import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class LedgerScreen extends ConsumerWidget {
  const LedgerScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final ledger = ref.watch(ledgerProvider);

    void onRefresh() {
      ref.invalidate(ledgerProvider);
    }

    return RefreshIndicator(
      onRefresh: () async => onRefresh(),
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          ledger.when(
            data: (state) => _LedgerSummaryCard(state: state),
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (e, _) => Text('Error loading ledger: $e'),
          ),
          const SizedBox(height: 16),
          ledger.when(
            data: (state) => _SetPhysicalLedgerCard(
              currentCashAed: (state.cashAed as num).toDouble(),
              currentGoldGrams: (state.goldGrams as num).toDouble(),
              onRefresh: onRefresh,
            ),
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
          const SizedBox(height: 16),
          _CompoundingTrackerCard(),
          const SizedBox(height: 16),
          _LedgerActionsCard(onRefresh: onRefresh),
        ],
      ),
    );
  }
}

class _LedgerSummaryCard extends StatelessWidget {
  const _LedgerSummaryCard({required this.state});

  final dynamic state;

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final tt = Theme.of(context).textTheme;
    final cashAed = (state.cashAed as num).toDouble();
    final goldGrams = (state.goldGrams as num).toDouble();
    final exposure = (state.openExposurePercent as num).toDouble();
    final deployable = (state.deployableCashAed as num).toDouble();
    final openBuys = state.openBuyCount as int;
    final bucketC1 = (state.bucketC1Aed as num).toDouble();
    final bucketC2 = (state.bucketC2Aed as num).toDouble();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Ledger State', style: tt.titleMedium),
            const SizedBox(height: 12),
            _row('Cash Balance', 'AED ${cashAed.toStringAsFixed(2)}', cs.primary),
            _row('Gold Holdings', '${goldGrams.toStringAsFixed(2)} g', cs.secondary),
            _row('Open Exposure', '${exposure.toStringAsFixed(1)} %',
                exposure > 60 ? cs.error : cs.onSurface),
            _row('Deployable Cash', 'AED ${deployable.toStringAsFixed(2)}', cs.primary),
            _row('Open Buy Count', openBuys.toString(), cs.onSurface),
            const Divider(height: 20),
            Text('Capacity Buckets (Section 4)', style: tt.labelMedium?.copyWith(color: cs.outline)),
            const SizedBox(height: 6),
            _row('C1 Bucket (80%)', 'AED ${bucketC1.toStringAsFixed(2)}', cs.primary),
            _row('C2 Bucket (20%)', 'AED ${bucketC2.toStringAsFixed(2)}', cs.secondary),
          ],
        ),
      ),
    );
  }

  Widget _row(String label, String value, Color color) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label),
            Text(value,
                style: TextStyle(fontWeight: FontWeight.bold, color: color)),
          ],
        ),
      );
}

/// CR12 — Set physical ledger (cash AED + gold g) from UI; values shown exactly, no scaling.
class _SetPhysicalLedgerCard extends ConsumerStatefulWidget {
  const _SetPhysicalLedgerCard({
    required this.currentCashAed,
    required this.currentGoldGrams,
    required this.onRefresh,
  });

  final double currentCashAed;
  final double currentGoldGrams;
  final VoidCallback onRefresh;

  @override
  ConsumerState<_SetPhysicalLedgerCard> createState() =>
      _SetPhysicalLedgerCardState();
}

class _SetPhysicalLedgerCardState extends ConsumerState<_SetPhysicalLedgerCard> {
  late final TextEditingController _cashController;
  late final TextEditingController _goldController;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _cashController = TextEditingController(
        text: widget.currentCashAed.toStringAsFixed(2));
    _goldController = TextEditingController(
        text: widget.currentGoldGrams.toStringAsFixed(2));
  }

  @override
  void didUpdateWidget(covariant _SetPhysicalLedgerCard oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.currentCashAed != widget.currentCashAed) {
      _cashController.text = widget.currentCashAed.toStringAsFixed(2);
    }
    if (oldWidget.currentGoldGrams != widget.currentGoldGrams) {
      _goldController.text = widget.currentGoldGrams.toStringAsFixed(2);
    }
  }

  @override
  void dispose() {
    _cashController.dispose();
    _goldController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() {
      _error = null;
      _busy = true;
    });
    try {
      final cash = double.tryParse(_cashController.text.replaceAll(',', '.'));
      final gold = double.tryParse(_goldController.text.replaceAll(',', '.'));
      if (cash == null || cash < 0 || gold == null || gold < 0) {
        setState(() {
          _error = 'Enter valid Cash (AED) and Gold (g)';
          _busy = false;
        });
        return;
      }
      await ref.read(brainApiProvider).ledgerSetPhysical(
            cashAed: cash,
            goldGrams: gold,
          );
      _cashController.text = cash.toStringAsFixed(2);
      _goldController.text = gold.toStringAsFixed(2);
      widget.onRefresh();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Physical ledger updated (cash and gold).')));
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = e.toString();
          _busy = false;
        });
      }
    }
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final tt = Theme.of(context).textTheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Set physical ledger (CR12)',
                style: tt.titleMedium),
            const SizedBox(height: 6),
            Text(
              'Enter cash (AED) and gold (g). Values are stored and displayed exactly (no scaling).',
              style: tt.bodySmall?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 12),
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Expanded(
                  child: TextField(
                    controller: _cashController,
                    keyboardType: const TextInputType.numberWithOptions(
                        decimal: true),
                    decoration: const InputDecoration(
                      labelText: 'Cash (AED)',
                      border: OutlineInputBorder(),
                      isDense: true,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextField(
                    controller: _goldController,
                    keyboardType: const TextInputType.numberWithOptions(
                        decimal: true),
                    decoration: const InputDecoration(
                      labelText: 'Gold (g)',
                      border: OutlineInputBorder(),
                      isDense: true,
                    ),
                  ),
                ),
              ],
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: _busy ? null : _submit,
              icon: _busy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.save),
              label: Text(_busy ? 'Updating…' : 'Update physical ledger'),
            ),
          ],
        ),
      ),
    );
  }
}

class _CompoundingTrackerCard extends ConsumerWidget {
  const _CompoundingTrackerCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final kpi = ref.watch(kpiProvider);
    final cs = Theme.of(context).colorScheme;
    final tt = Theme.of(context).textTheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Compounding Tracker (4x)', style: tt.titleMedium),
            const SizedBox(height: 12),
            kpi.when(
              data: (stats) {
                final c = stats.compounding;
                final progress = (c.multiple / 4.0).clamp(0.0, 1.0);
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _row('Starting Investment',
                        'AED ${c.startingInvestmentAed.toStringAsFixed(2)}',
                        cs.onSurface),
                    _row('Current Equity',
                        'AED ${c.currentEquityAed.toStringAsFixed(2)}',
                        cs.primary),
                    _row('Multiple', '${c.multiple.toStringAsFixed(2)}x',
                        cs.secondary),
                    const SizedBox(height: 10),
                    LinearProgressIndicator(
                      value: progress,
                      backgroundColor: cs.surfaceContainerHighest,
                      color:
                          c.milestoneReached ? cs.tertiary : cs.primary,
                      minHeight: 10,
                      borderRadius: BorderRadius.circular(6),
                    ),
                    const SizedBox(height: 6),
                    if (c.milestoneReached)
                      Text(
                        '🎉 4x REACHED — Ready to Pull Original Capital',
                        style: TextStyle(
                            color: cs.tertiary,
                            fontWeight: FontWeight.bold),
                      )
                    else
                      Text(
                        '4x Target: AED ${c.neededForFourXAed.toStringAsFixed(2)} remaining',
                        style: tt.bodySmall,
                      ),
                  ],
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => Text('KPI error: $e'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _row(String label, String value, Color color) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label),
            Text(value,
                style: TextStyle(fontWeight: FontWeight.bold, color: color)),
          ],
        ),
      );
}

class _LedgerActionsCard extends ConsumerStatefulWidget {
  const _LedgerActionsCard({required this.onRefresh});
  final VoidCallback onRefresh;

  @override
  ConsumerState<_LedgerActionsCard> createState() => _LedgerActionsCardState();
}

class _LedgerActionsCardState extends ConsumerState<_LedgerActionsCard> {
  bool _busy = false;

  Future<void> _performAction(
    Future<void> Function() action, {
    required String successMsg,
  }) async {
    if (_busy) return;
    setState(() => _busy = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await action();
      widget.onRefresh();
      messenger.showSnackBar(SnackBar(content: Text(successMsg)));
    } catch (e) {
      messenger.showSnackBar(SnackBar(content: Text('Failed: $e')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _showAmountDialog({
    required String title,
    required String actionLabel,
    required Future<void> Function(double amount, String note) onConfirm,
    bool isAdjustment = false,
  }) async {
    final amountController = TextEditingController();
    final noteController = TextEditingController();

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: amountController,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true, signed: true),
              decoration: InputDecoration(
                labelText:
                    isAdjustment ? 'Adjustment AED (+/-)' : 'Amount AED',
                hintText: isAdjustment ? 'e.g. 500 or -200' : 'e.g. 50000',
              ),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: noteController,
              decoration: const InputDecoration(
                labelText: 'Note (optional)',
                hintText: 'e.g. Shop slip #12',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text(actionLabel),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    final amount = double.tryParse(amountController.text.trim());
    final note = noteController.text.trim();
    amountController.dispose();
    noteController.dispose();

    if (amount == null || amount == 0) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Please enter a valid non-zero amount.')),
        );
      }
      return;
    }

    await onConfirm(amount, note);
  }

  @override
  Widget build(BuildContext context) {
    final api = ref.read(brainApiProvider);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Capital Actions',
                style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 8,
              children: [
                FilledButton.icon(
                  onPressed: _busy
                      ? null
                      : () => _showAmountDialog(
                            title: 'Add Capital (Deposit)',
                            actionLabel: 'Deposit',
                            onConfirm: (amount, note) => _performAction(
                              () async {
                                await api.ledgerDeposit(
                                    amountAed: amount, note: note);
                              },
                              successMsg:
                                  'Deposit of AED ${amount.toStringAsFixed(2)} recorded.',
                            ),
                          ),
                  icon: const Icon(Icons.add),
                  label: const Text('Add Capital'),
                ),
                FilledButton.icon(
                  onPressed: _busy
                      ? null
                      : () => _showAmountDialog(
                            title: 'Withdraw Capital',
                            actionLabel: 'Withdraw',
                            onConfirm: (amount, note) => _performAction(
                              () async {
                                await api.ledgerWithdraw(
                                    amountAed: amount, note: note);
                              },
                              successMsg:
                                  'Withdrawal of AED ${amount.toStringAsFixed(2)} recorded.',
                            ),
                          ),
                  icon: const Icon(Icons.remove),
                  label: const Text('Withdraw Capital'),
                  style: FilledButton.styleFrom(
                    backgroundColor:
                        Theme.of(context).colorScheme.secondaryContainer,
                    foregroundColor:
                        Theme.of(context).colorScheme.onSecondaryContainer,
                  ),
                ),
                OutlinedButton.icon(
                  onPressed: _busy
                      ? null
                      : () => _showAmountDialog(
                            title: 'Shop Adjustment',
                            actionLabel: 'Apply',
                            isAdjustment: true,
                            onConfirm: (amount, note) => _performAction(
                              () async {
                                await api.ledgerAdjustment(
                                    adjustmentAed: amount, note: note);
                              },
                              successMsg:
                                  'Adjustment of AED ${amount.toStringAsFixed(2)} applied.',
                            ),
                          ),
                  icon: const Icon(Icons.tune),
                  label: const Text('Shop Adjustment'),
                ),
              ],
            ),
            if (_busy) ...[
              const SizedBox(height: 12),
              const LinearProgressIndicator(),
            ],
          ],
        ),
      ),
    );
  }
}
