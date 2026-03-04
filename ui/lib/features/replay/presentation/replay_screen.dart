import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class ReplayScreen extends ConsumerStatefulWidget {
  const ReplayScreen({super.key});

  @override
  ConsumerState<ReplayScreen> createState() => _ReplayScreenState();
}

class _ReplayScreenState extends ConsumerState<ReplayScreen> {
  bool _starting = false;
  bool _pausing = false;
  bool _resuming = false;
  bool _stopping = false;
  bool _useMockAi = false;
  int _speedMultiplier = 100;
  final TextEditingController _symbolController = TextEditingController(text: 'XAUUSD');

  @override
  void dispose() {
    _symbolController.dispose();
    super.dispose();
  }

  Future<void> _refresh() async {
    ref
      ..invalidate(replayStatusProvider)
      ..invalidate(timelineProvider);
  }

  Future<void> _startReplay() async {
    if (_starting) return;
    setState(() => _starting = true);
    final messenger = ScaffoldMessenger.of(context);

    try {
      await ref.read(brainApiProvider).startReplay(
            symbol: _symbolController.text.trim().toUpperCase(),
            speedMultiplier: _speedMultiplier,
            useAI: true,
            useMockAI: _useMockAi,
          );
      await _refresh();
      messenger.showSnackBar(
        SnackBar(
          content: Text(_useMockAi
              ? 'Replay started with explicit mock AI mode.'
              : 'Replay started with real AI mode.'),
        ),
      );
    } catch (error) {
      messenger.showSnackBar(SnackBar(content: Text('Failed to start replay: $error')));
    } finally {
      if (mounted) {
        setState(() => _starting = false);
      }
    }
  }

  Future<void> _pauseReplay() async {
    if (_pausing) return;
    setState(() => _pausing = true);
    try {
      await ref.read(brainApiProvider).pauseReplay();
      await _refresh();
    } finally {
      if (mounted) setState(() => _pausing = false);
    }
  }

  Future<void> _resumeReplay() async {
    if (_resuming) return;
    setState(() => _resuming = true);
    try {
      await ref.read(brainApiProvider).resumeReplay();
      await _refresh();
    } finally {
      if (mounted) setState(() => _resuming = false);
    }
  }

  Future<void> _stopReplay() async {
    if (_stopping) return;
    setState(() => _stopping = true);
    try {
      await ref.read(brainApiProvider).stopReplay();
      await _refresh();
    } finally {
      if (mounted) setState(() => _stopping = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final replay = ref.watch(replayStatusProvider);
    final timeline = ref.watch(timelineProvider);

    return RefreshIndicator(
      onRefresh: _refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Historical Replay', style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  TextField(
                    controller: _symbolController,
                    decoration: const InputDecoration(
                      labelText: 'Symbol',
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      const Text('Speed'),
                      Expanded(
                        child: Slider(
                          value: _speedMultiplier.toDouble().clamp(1, 500),
                          min: 1,
                          max: 500,
                          divisions: 99,
                          label: _speedMultiplier.toString(),
                          onChanged: (value) => setState(() => _speedMultiplier = value.round()),
                        ),
                      ),
                      Text('${_speedMultiplier}x'),
                    ],
                  ),
                  SwitchListTile.adaptive(
                    contentPadding: EdgeInsets.zero,
                    value: _useMockAi,
                    onChanged: (value) => setState(() => _useMockAi = value),
                    title: const Text('Use mock AI (explicit)'),
                    subtitle: const Text('When off, replay uses real AI by default.'),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      FilledButton(
                        onPressed: _starting ? null : _startReplay,
                        child: Text(_starting ? 'Starting...' : 'Start'),
                      ),
                      OutlinedButton(
                        onPressed: _pausing ? null : _pauseReplay,
                        child: Text(_pausing ? 'Pausing...' : 'Pause'),
                      ),
                      OutlinedButton(
                        onPressed: _resuming ? null : _resumeReplay,
                        child: Text(_resuming ? 'Resuming...' : 'Resume'),
                      ),
                      OutlinedButton(
                        onPressed: _stopping ? null : _stopReplay,
                        child: Text(_stopping ? 'Stopping...' : 'Stop'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          replay.when(
            data: (data) => Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _chip('Running', data.status.isRunning ? 'YES' : 'NO'),
                    _chip('Paused', data.status.isPaused ? 'YES' : 'NO'),
                    _chip('Driver TF', data.status.driverTimeframe),
                    _chip('Processed', '${data.status.processedCandles}/${data.status.totalCandles}'),
                    _chip('Cycles', data.status.cyclesTriggered.toString()),
                    _chip('Setups', data.status.setupCandidatesFound.toString()),
                    _chip('Trades Armed', data.status.tradesArmed.toString()),
                  ],
                ),
              ),
            ),
            loading: () => const LinearProgressIndicator(),
            error: (error, _) => Text('Replay status error: $error'),
          ),
          const SizedBox(height: 12),
          Text('Timeline (latest 200)', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          timeline.when(
            data: (items) {
              if (items.isEmpty) {
                return const Card(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No timeline events yet.'),
                  ),
                );
              }

              return Column(
                children: items.reversed.take(80).map((item) {
                  return Card(
                    child: ListTile(
                      dense: true,
                      title: Text('${item.eventType} • ${item.stage}'),
                      subtitle: Text(
                        '${item.createdAtUtc.toIso8601String()}\n'
                        'cycle=${item.cycleId ?? '-'} • source=${item.source}',
                      ),
                    ),
                  );
                }).toList(),
              );
            },
            loading: () => const LinearProgressIndicator(),
            error: (error, _) => Text('Timeline error: $error'),
          ),
        ],
      ),
    );
  }

  Widget _chip(String label, String value) {
    return Chip(label: Text('$label: $value'));
  }
}
