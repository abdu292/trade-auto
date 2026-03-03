import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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

  @override
  Widget build(BuildContext context) {
    final riskProfiles = ref.watch(riskProfilesProvider);
    final hazardWindows = ref.watch(hazardWindowsProvider);

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
          ..invalidate(runtimeStatusProvider);
      },
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
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
                      (item) => Card(
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
                      (item) => Card(
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
