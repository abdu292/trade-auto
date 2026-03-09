import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/models.dart';
import '../../../presentation/app_providers.dart';

class RiskControlScreen extends ConsumerStatefulWidget {
  const RiskControlScreen({super.key});

  @override
  ConsumerState<RiskControlScreen> createState() => _RiskControlScreenState();
}

class _RiskControlScreenState extends ConsumerState<RiskControlScreen> {
  final TextEditingController _titleController =
      TextEditingController(text: 'High-impact news');

  String _selectedCategory = 'EVENT';
  int _selectedDurationMinutes = 60;
  bool _isCreatingHazard = false;
  bool _isTogglingAutoTrade = false;
  bool _isTriggeringPanic = false;
  bool _isUpdatingMinGrams = false;
  final Set<String> _disablingHazardIds = <String>{};

  static const List<String> _categories = <String>[
    'EVENT',
    'NFP',
    'CPI',
    'FOMC',
    'NEWS',
  ];

  static const List<int> _durationMinutes = <int>[15, 30, 60, 120, 240];

  @override
  void dispose() {
    _titleController.dispose();
    super.dispose();
  }

  Future<void> _createHazardWindow() async {
    if (_isCreatingHazard) {
      return;
    }

    final messenger = ScaffoldMessenger.of(context);
    final title = _titleController.text.trim();
    if (title.isEmpty) {
      messenger.showSnackBar(
        const SnackBar(content: Text('Please enter a title.')),
      );
      return;
    }

    setState(() => _isCreatingHazard = true);
    try {
      final now = DateTime.now().toUtc();
      final end = now.add(Duration(minutes: _selectedDurationMinutes));
      await ref.read(brainApiProvider).createHazardWindow(
            title: title,
            category: _selectedCategory,
            startUtc: now,
            endUtc: end,
          );
      ref
        ..invalidate(hazardWindowsProvider)
        ..invalidate(runtimeStatusProvider);
      messenger.showSnackBar(
        SnackBar(
          content:
              Text('Hazard block created for $_selectedDurationMinutes minutes.'),
        ),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to create hazard block: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isCreatingHazard = false);
      }
    }
  }

  Future<void> _disableHazardWindow(String id) async {
    if (_disablingHazardIds.contains(id)) {
      return;
    }

    setState(() => _disablingHazardIds.add(id));
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(brainApiProvider).disableHazardWindow(id);
      ref
        ..invalidate(hazardWindowsProvider)
        ..invalidate(runtimeStatusProvider);
      messenger.showSnackBar(
        const SnackBar(content: Text('Hazard window removed.')),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to remove hazard window: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _disablingHazardIds.remove(id));
      }
    }
  }

  Future<void> _toggleAutoTrade(bool currentValue) async {
    if (_isTogglingAutoTrade) return;

    final newValue = !currentValue;
    final messenger = ScaffoldMessenger.of(context);

    // Require confirmation when enabling Auto Trade
    if (newValue) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Enable Auto Trade?'),
          content: const Text(
            'When Auto Trade is ON, the system will automatically route ARMED trades '
            'directly to MT5 for execution without manual approval — as long as all '
            'core laws pass.\n\n'
            'Make sure you are comfortable with the current risk settings before enabling this.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Enable Auto Trade'),
            ),
          ],
        ),
      );
      if (confirmed != true) return;
    }

    setState(() => _isTogglingAutoTrade = true);
    try {
      await ref.read(brainApiProvider).setAutoTradeEnabled(newValue);
      ref.invalidate(runtimeSettingsProvider);
      messenger.showSnackBar(
        SnackBar(
          content: Text(newValue
              ? '✅ Auto Trade ENABLED — trades will be sent to MT5 automatically.'
              : '⏸ Auto Trade DISABLED — trades will go to approval queue.'),
          duration: const Duration(seconds: 4),
        ),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to toggle Auto Trade: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isTogglingAutoTrade = false);
      }
    }
  }

  Future<void> _updateMinTradeGrams(double currentValue) async {
    if (_isUpdatingMinGrams) return;

    final controller = TextEditingController(
      text: currentValue % 1 == 0 ? currentValue.toStringAsFixed(0) : currentValue.toStringAsFixed(2),
    );
    final messenger = ScaffoldMessenger.of(context);

    final result = await showDialog<double>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Set Min Trade Grams'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Set the minimum trade size in grams. Orders below this threshold '
              'are rejected by the decision engine.\n\nDefault: 100 g.',
              style: TextStyle(fontSize: 13),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(
                labelText: 'Min Grams',
                hintText: 'e.g. 50 or 0.5',
                border: OutlineInputBorder(),
                suffixText: 'g',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              final v = double.tryParse(controller.text.trim());
              if (v != null && v > 0) {
                Navigator.pop(ctx, v);
              }
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );

    controller.dispose();

    if (result == null || !mounted) return;

    setState(() => _isUpdatingMinGrams = true);
    try {
      await ref.read(brainApiProvider).setMinTradeGrams(result);
      ref.invalidate(runtimeSettingsProvider);
      messenger.showSnackBar(
        SnackBar(content: Text('Min trade grams updated to ${result % 1 == 0 ? result.toStringAsFixed(0) : result.toStringAsFixed(2)} g.')),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to update min trade grams: $error')),
      );
    } finally {
      if (mounted) setState(() => _isUpdatingMinGrams = false);
    }
  }

  Future<void> _triggerPanicInterrupt() async {
    if (_isTriggeringPanic) return;

    final messenger = ScaffoldMessenger.of(context);

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Row(
          children: [
            Icon(Icons.warning_amber, color: Theme.of(ctx).colorScheme.error),
            const SizedBox(width: 8),
            const Text('Global Panic Interrupt'),
          ],
        ),
        content: const Text(
          'This will:\n'
          '• Cancel ALL pending orders immediately\n'
          '• Send cancel signal to the MT5 EA\n'
          '• Freeze new releases briefly\n\n'
          'Use only when FAIL is threatened, sudden liquidation pattern, '
          'or macro shock is detected.\n\n'
          'Are you sure?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(ctx).colorScheme.error,
            ),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('TRIGGER PANIC INTERRUPT'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    setState(() => _isTriggeringPanic = true);
    try {
      final result = await ref.read(brainApiProvider).triggerPanicInterrupt();
      ref
        ..invalidate(runtimeStatusProvider)
        ..invalidate(activeTradesProvider);
      final message = result['message']?.toString() ?? 'Panic interrupt executed.';
      messenger.showSnackBar(
        SnackBar(
          content: Text('🚨 $message'),
          backgroundColor: Theme.of(context).colorScheme.errorContainer,
          duration: const Duration(seconds: 6),
        ),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to trigger panic interrupt: $error')),
      );
    } finally {
      if (mounted) {
        setState(() => _isTriggeringPanic = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final riskProfiles = ref.watch(riskProfilesProvider);
    final hazardWindows = ref.watch(hazardWindowsProvider);
    final runtimeSettings = ref.watch(runtimeSettingsProvider);

    Future<void> activate(String id) async {
      final messenger = ScaffoldMessenger.of(context);
      try {
        await ref.read(brainApiProvider).activateRisk(id);
        ref.invalidate(riskProfilesProvider);
      } catch (error) {
        messenger.showSnackBar(
            SnackBar(content: Text('Failed to activate risk profile: $error')));
      }
    }

    return RefreshIndicator(
      onRefresh: () async {
        ref
          ..invalidate(riskProfilesProvider)
          ..invalidate(hazardWindowsProvider)
          ..invalidate(runtimeStatusProvider)
          ..invalidate(runtimeSettingsProvider);
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // ── Auto Trade Toggle ───────────────────────────────────────────────
          _AutoTradeToggleCard(
            runtimeSettings: runtimeSettings,
            isToggling: _isTogglingAutoTrade,
            onToggle: _toggleAutoTrade,
          ),
          const SizedBox(height: 12),

          // ── Min Trade Grams Configuration ────────────────────────────────────
          _MinTradeGramsCard(
            runtimeSettings: runtimeSettings,
            isUpdating: _isUpdatingMinGrams,
            onEdit: _updateMinTradeGrams,
          ),
          const SizedBox(height: 12),

          // ── Global Panic Interrupt ──────────────────────────────────────────
          _PanicInterruptCard(
            isTriggeringPanic: _isTriggeringPanic,
            onTrigger: _triggerPanicInterrupt,
          ),
          const SizedBox(height: 12),

          // Anomaly Alerts
          _AnomalyAlertsCard(),
          const SizedBox(height: 12),
          Text('Risk Profiles',
              style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          riskProfiles.when(
            data: (items) {
              if (items.isEmpty) {
                return const Card(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No risk profiles available.'),
                  ),
                );
              }

              return Column(
                children: items
                    .map(
                      (item) => Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Card(
                          child: ListTile(
                            title: Text(item.name),
                            subtitle: Text(
                                'Level ${item.level} • Max DD ${item.maxDrawdownPercent.toStringAsFixed(2)}%'),
                            trailing: item.isActive
                                ? const Chip(label: Text('Active'))
                                : FilledButton(
                                    onPressed: () => activate(item.id),
                                    child: const Text('Activate'),
                                  ),
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
                child: Text('Error loading risk profiles: $error'),
              ),
            ),
          ),
          const SizedBox(height: 20),
          Text('Hazard Windows',
              style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Quick Block',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  const Text(
                    'Use this when major news is expected. Trading will be blocked during this window.',
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _titleController,
                    decoration: const InputDecoration(
                      labelText: 'Title',
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: DropdownButtonFormField<String>(
                          initialValue: _selectedCategory,
                          decoration: const InputDecoration(
                            labelText: 'Category',
                            border: OutlineInputBorder(),
                          ),
                          items: _categories
                              .map(
                                (item) => DropdownMenuItem<String>(
                                  value: item,
                                  child: Text(item),
                                ),
                              )
                              .toList(),
                          onChanged: (value) {
                            if (value == null) {
                              return;
                            }
                            setState(() => _selectedCategory = value);
                          },
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: DropdownButtonFormField<int>(
                          initialValue: _selectedDurationMinutes,
                          decoration: const InputDecoration(
                            labelText: 'Duration',
                            border: OutlineInputBorder(),
                          ),
                          items: _durationMinutes
                              .map(
                                (minutes) => DropdownMenuItem<int>(
                                  value: minutes,
                                  child: Text('$minutes min'),
                                ),
                              )
                              .toList(),
                          onChanged: (value) {
                            if (value == null) {
                              return;
                            }
                            setState(() => _selectedDurationMinutes = value);
                          },
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      onPressed: _isCreatingHazard ? null : _createHazardWindow,
                      icon: _isCreatingHazard
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.block),
                      label: Text(_isCreatingHazard
                          ? 'Creating...'
                          : 'Create Hazard Block Now'),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          hazardWindows.when(
            data: (items) {
              if (items.isEmpty) {
                return const Card(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No hazard windows created yet.'),
                  ),
                );
              }

              return Column(
                children: items
                    .take(20)
                    .map(
                      (item) => Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Card(
                          child: ListTile(
                            title: Text(item.title),
                            subtitle: Text(
                              '${item.category} • ${item.startUtc.toLocal()} → ${item.endUtc.toLocal()}',
                            ),
                            trailing: item.isActive
                                ? Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      const Chip(label: Text('Active')),
                                      const SizedBox(width: 8),
                                      FilledButton.tonal(
                                        onPressed:
                                            _disablingHazardIds.contains(item.id)
                                                ? null
                                                : () =>
                                                    _disableHazardWindow(item.id),
                                        child: _disablingHazardIds.contains(item.id)
                                            ? const SizedBox(
                                                width: 14,
                                                height: 14,
                                                child: CircularProgressIndicator(
                                                    strokeWidth: 2),
                                              )
                                            : const Text('Remove'),
                                      ),
                                    ],
                                  )
                                : const Chip(label: Text('Scheduled')),
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
                child: Text('Error loading hazard windows: $error'),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ── Auto Trade Toggle Card ─────────────────────────────────────────────────────

class _AutoTradeToggleCard extends StatelessWidget {
  const _AutoTradeToggleCard({
    required this.runtimeSettings,
    required this.isToggling,
    required this.onToggle,
  });

  final AsyncValue<RuntimeSettings> runtimeSettings;
  final bool isToggling;
  final void Function(bool currentValue) onToggle;

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.smart_toy, color: cs.primary),
                const SizedBox(width: 8),
                Text(
                  'Auto Trade',
                  style: Theme.of(context)
                      .textTheme
                      .titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
              ],
            ),
            const SizedBox(height: 8),
            const Text(
              'When ON, ARMED trades are routed directly to MT5 for automatic execution '
              'without manual approval — as long as all core laws pass.\n\n'
              'Default: OFF. Enable only when you are comfortable with the current settings.',
              style: TextStyle(fontSize: 13),
            ),
            const SizedBox(height: 12),
            runtimeSettings.when(
              data: (settings) {
                final enabled = settings.autoTradeEnabled;
                return Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            enabled ? '✅ Auto Trade is ON' : '⏸ Auto Trade is OFF',
                            style: TextStyle(
                              fontWeight: FontWeight.w600,
                              color: enabled ? Colors.green.shade700 : cs.onSurfaceVariant,
                            ),
                          ),
                          Text(
                            enabled
                                ? 'Trades go directly to MT5 queue'
                                : 'Trades go to approval queue',
                            style: Theme.of(context)
                                .textTheme
                                .bodySmall
                                ?.copyWith(color: cs.onSurfaceVariant),
                          ),
                        ],
                      ),
                    ),
                    if (isToggling)
                      const SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    else
                      Switch(
                        value: enabled,
                        onChanged: (_) => onToggle(enabled),
                        activeColor: Colors.green.shade600,
                      ),
                  ],
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text(
                'Could not load Auto Trade status: $error',
                style: TextStyle(color: cs.error),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Min Trade Grams Card ───────────────────────────────────────────────────────

class _MinTradeGramsCard extends StatelessWidget {
  const _MinTradeGramsCard({
    required this.runtimeSettings,
    required this.isUpdating,
    required this.onEdit,
  });

  final AsyncValue<RuntimeSettings> runtimeSettings;
  final bool isUpdating;
  final void Function(double currentValue) onEdit;

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.scale, color: cs.primary),
                const SizedBox(width: 8),
                Text(
                  'Min Trade Grams',
                  style: Theme.of(context)
                      .textTheme
                      .titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
              ],
            ),
            const SizedBox(height: 8),
            const Text(
              'Minimum gram quantity for any trade. Orders calculated below this threshold '
              'are rejected automatically.\n\nDefault: 100 g. Lower to test small trades.',
              style: TextStyle(fontSize: 13),
            ),
            const SizedBox(height: 12),
            runtimeSettings.when(
              data: (settings) {
                final grams = settings.minTradeGrams;
                return Row(
                  children: [
                    Expanded(
                      child: Text(
                        '${grams % 1 == 0 ? grams.toStringAsFixed(0) : grams.toStringAsFixed(2)} g',
                        style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                              fontWeight: FontWeight.w700,
                              color: cs.primary,
                            ),
                      ),
                    ),
                    if (isUpdating)
                      const SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    else
                      OutlinedButton.icon(
                        onPressed: () => onEdit(grams),
                        icon: const Icon(Icons.edit, size: 16),
                        label: const Text('Change'),
                      ),
                  ],
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text(
                'Could not load min trade grams: $error',
                style: TextStyle(color: cs.error),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Global Panic Interrupt Card ───────────────────────────────────────────────

class _PanicInterruptCard extends StatelessWidget {
  const _PanicInterruptCard({
    required this.isTriggeringPanic,
    required this.onTrigger,
  });

  final bool isTriggeringPanic;
  final VoidCallback onTrigger;

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;

    return Card(
      color: cs.errorContainer.withOpacity(0.3),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.warning_amber, color: cs.error),
                const SizedBox(width: 8),
                Text(
                  'Global Panic Interrupt',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: cs.error,
                      ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            const Text(
              'Immediately cancels ALL pending orders and sends a cancel signal to the '
              'MT5 EA. Use only when FAIL is threatened, sudden liquidation pattern, '
              'spread explosion, or macro shock is detected.',
              style: TextStyle(fontSize: 13),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                style: FilledButton.styleFrom(
                  backgroundColor: cs.error,
                  foregroundColor: cs.onError,
                ),
                onPressed: isTriggeringPanic ? null : onTrigger,
                icon: isTriggeringPanic
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.dangerous),
                label: Text(
                    isTriggeringPanic ? 'Triggering...' : 'Trigger Panic Interrupt'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AnomalyAlertsCard extends ConsumerWidget {
  const _AnomalyAlertsCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final runtime = ref.watch(runtimeStatusProvider);
    final cs = Theme.of(context).colorScheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Anomaly Alerts',
                style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            runtime.when(
              data: (rt) {
                final alerts = <_Alert>[];

                if (rt.mt5ServerTime != null &&
                    (rt.freezeGapDetected || rt.tickRatePer30s == 0)) {
                  alerts.add(_Alert(
                    icon: Icons.signal_wifi_off,
                    label: rt.freezeGapDetected
                        ? 'Tick drought: freeze gap detected'
                        : 'Tick drought: no ticks received (rate=0)',
                    color: cs.error,
                  ));
                }

                final spreadSpike = rt.spreadMedian60m > 0 &&
                    rt.spread > 2 * rt.spreadMedian60m;
                if (spreadSpike) {
                  alerts.add(_Alert(
                    icon: Icons.trending_up,
                    label:
                        'Spread spike: ${rt.spread.toStringAsFixed(3)} > 2× median ${rt.spreadMedian60m.toStringAsFixed(3)}',
                    color: cs.error,
                  ));
                }

                if (rt.panicSuspected) {
                  alerts.add(_Alert(
                    icon: Icons.warning_amber,
                    label: 'Telegram panic surge detected',
                    color: cs.error,
                  ));
                }

                if (rt.macroCacheAgeMinutes > 120) {
                  alerts.add(_Alert(
                    icon: Icons.cloud_off,
                    label:
                        'Macro cache stale: ${rt.macroCacheAgeMinutes}m ago',
                    color: cs.tertiary,
                  ));
                }

                if (rt.mt5ServerTime == null) {
                  alerts.add(_Alert(
                    icon: Icons.sync_problem,
                    label: 'MT5 desync: server time unavailable',
                    color: cs.error,
                  ));
                }

                if (alerts.isEmpty) {
                  return Row(
                    children: [
                      Icon(Icons.check_circle, color: cs.primary, size: 18),
                      const SizedBox(width: 8),
                      const Text('No anomalies detected'),
                    ],
                  );
                }

                return Column(
                  children: alerts
                      .map(
                        (a) => Padding(
                          padding: const EdgeInsets.symmetric(vertical: 3),
                          child: Row(
                            children: [
                              Icon(a.icon, color: a.color, size: 18),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(
                                  a.label,
                                  style: TextStyle(color: a.color),
                                ),
                              ),
                            ],
                          ),
                        ),
                      )
                      .toList(),
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (error, _) => Text('Runtime error: $error'),
            ),
          ],
        ),
      ),
    );
  }
}

class _Alert {
  const _Alert({
    required this.icon,
    required this.label,
    required this.color,
  });

  final IconData icon;
  final String label;
  final Color color;
}
