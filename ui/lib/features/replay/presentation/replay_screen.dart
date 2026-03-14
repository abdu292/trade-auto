import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/models.dart';
import '../../../presentation/app_providers.dart';

// ─── Phase helpers ────────────────────────────────────────────────────────────

enum _ReplayPhase {
  idle,
  mt5FetchQueued,
  mt5FetchReceived,
  importing,
  running,
  paused,
  done,
  error,
}

_ReplayPhase _parsePhase(String phase) {
  return switch (phase.toUpperCase()) {
    'MT5_FETCH_QUEUED' => _ReplayPhase.mt5FetchQueued,
    'MT5_FETCH_RECEIVED' => _ReplayPhase.mt5FetchReceived,
    'IMPORTING' => _ReplayPhase.importing,
    'RUNNING' => _ReplayPhase.running,
    'PAUSED' => _ReplayPhase.paused,
    'DONE' => _ReplayPhase.done,
    'ERROR' => _ReplayPhase.error,
    _ => _ReplayPhase.idle,
  };
}

// ─── Screen ───────────────────────────────────────────────────────────────────

class ReplayScreen extends ConsumerStatefulWidget {
  const ReplayScreen({super.key});

  @override
  ConsumerState<ReplayScreen> createState() => _ReplayScreenState();
}

class _ReplayScreenState extends ConsumerState<ReplayScreen> {
  bool _useMockAi = true;
  bool _useLiveNewsAndTelegramInReplay = false;
  int _speedMultiplier = 100;
  final TextEditingController _initialCashController =
      TextEditingController(text: '350000');
  final TextEditingController _symbolController =
  TextEditingController(text: 'XAUUSD.gram');

  DateTime _fromDate = DateTime.now().subtract(const Duration(days: 7));
  DateTime _toDate = DateTime.now();

  bool _running = false;
  bool _pausing = false;
  bool _resuming = false;
  bool _stopping = false;
  bool _awaitingAutoStart = false;
  bool _autoStartBusy = false;

  Timer? _autoRefreshTimer;

  @override
  void initState() {
    super.initState();
    _startAutoRefresh();
  }

  @override
  void dispose() {
    _initialCashController.dispose();
    _symbolController.dispose();
    _autoRefreshTimer?.cancel();
    super.dispose();
  }

  double _initialCashValue() {
    final parsed = double.tryParse(_initialCashController.text.trim());
    if (parsed == null || parsed <= 0) {
      return 350000;
    }

    return parsed;
  }

  void _startAutoRefresh() {
    _autoRefreshTimer?.cancel();
    _autoRefreshTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      ref.invalidate(replayStatusProvider);
      _checkAutoStartIfReady();
    });
  }

  Future<void> _refresh() async {
    ref
      ..invalidate(replayStatusProvider)
      ..invalidate(timelineProvider);
  }

  Future<void> _pickFromDate() async {
    final date = await showDatePicker(
      context: context,
      initialDate: _fromDate,
      firstDate: DateTime(2020),
      lastDate: DateUtils.dateOnly(_toDate),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_fromDate),
    );
    if (!mounted) return;
    final candidate = DateTime(date.year, date.month, date.day,
        time?.hour ?? _fromDate.hour, time?.minute ?? _fromDate.minute);
    if (!candidate.isBefore(_toDate)) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('From time must be earlier than To time.'),
          ),
        );
      }
      return;
    }
    setState(() => _fromDate = candidate);
  }

  Future<void> _pickToDate() async {
    final date = await showDatePicker(
      context: context,
      initialDate: _toDate,
      firstDate: DateUtils.dateOnly(_fromDate),
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_toDate),
    );
    if (!mounted) return;
    final candidate = DateTime(date.year, date.month, date.day,
        time?.hour ?? _toDate.hour, time?.minute ?? _toDate.minute);
    if (!candidate.isAfter(_fromDate)) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('To time must be later than From time.'),
          ),
        );
      }
      return;
    }
    setState(() => _toDate = candidate);
  }

  Future<void> _runReplay() async {
    if (_running) return;
    if (!_fromDate.isBefore(_toDate)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
              'Please choose a valid range: From must be earlier than To.'),
        ),
      );
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        icon: const Icon(Icons.info_outline, size: 36),
        title: const Text('Start Replay'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('This action will:',
                style: TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            const Text('1. Request historical candle data from MT5'),
            const Text('2. Import M5, M15, H1 candles automatically'),
            const Text('3. Start the replay engine'),
            const SizedBox(height: 12),
            const Text(
              'The MT5 EA must be running and connected.\n'
              'Real trade execution is always disabled during replay.',
              style: TextStyle(fontSize: 12),
            ),
          ],
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel')),
          FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Run Replay')),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    setState(() => _running = true);
    final messenger = ScaffoldMessenger.of(context);
    final symbol = _symbolController.text.trim();

    try {
      // from/to sent as UTC (device converts local time); works for India, KSA, UAE, etc.
      await ref.read(brainApiProvider).runReplay(
            symbol: symbol,
            from: _fromDate,
            to: _toDate,
            speedMultiplier: _speedMultiplier,
            useMockAI: _useMockAi,
            initialCashAed: _initialCashValue(),
            useLiveNewsAndTelegramInReplay: _useLiveNewsAndTelegramInReplay,
          );
      if (mounted) {
        setState(() {
          _awaitingAutoStart = true;
        });
      }
      await _refresh();
      messenger.showSnackBar(const SnackBar(
          content: Text(
              'MT5 history fetch queued. Waiting for EA to deliver data…')));
      await _checkAutoStartIfReady();
    } catch (error) {
      messenger.showSnackBar(
          SnackBar(content: Text('Failed to start replay: $error')));
    } finally {
      if (mounted) setState(() => _running = false);
    }
  }

  Future<void> _startManualReplay() async {
    if (_running) return;
    setState(() => _running = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(brainApiProvider).startReplay(
        symbol: _symbolController.text.trim(),
            speedMultiplier: _speedMultiplier,
            useAI: !_useMockAi,
            useMockAI: _useMockAi,
            from: _fromDate,
            to: _toDate,
            useLiveNewsAndTelegramInReplay: _useLiveNewsAndTelegramInReplay,
          );
      await _refresh();
      messenger.showSnackBar(const SnackBar(
          content: Text('Replay started using imported candles.')));
    } catch (error) {
      messenger.showSnackBar(
          SnackBar(content: Text('Failed to start replay: $error')));
    } finally {
      if (mounted) setState(() => _running = false);
    }
  }

  Future<void> _checkAutoStartIfReady() async {
    if (!_awaitingAutoStart || _autoStartBusy || !mounted) return;

    _autoStartBusy = true;
    try {
      final symbol = _symbolController.text.trim();
      final statusResponse =
          await ref.read(brainApiProvider).getReplayStatus(symbol: symbol);
      final phase = _parsePhase(statusResponse.status.phase);
      final imported = statusResponse.importedCandles;
      final hasAllTimeframes = imported.containsKey('M5') &&
          imported.containsKey('M15') &&
          imported.containsKey('H1');

      if (phase == _ReplayPhase.mt5FetchReceived ||
          phase == _ReplayPhase.importing ||
          (phase == _ReplayPhase.mt5FetchQueued && hasAllTimeframes)) {
        await ref.read(brainApiProvider).startAfterFetch(
              symbol: symbol,
              from: _fromDate,
              to: _toDate,
              speedMultiplier: _speedMultiplier,
              useMockAI: _useMockAi,
              initialCashAed: _initialCashValue(),
              useLiveNewsAndTelegramInReplay: _useLiveNewsAndTelegramInReplay,
            );
        if (mounted) {
          setState(() {
            _awaitingAutoStart = false;
          });
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
                content:
                    Text('Replay started automatically after MT5 import.')),
          );
        }
        await _refresh();
      } else if (phase == _ReplayPhase.running || phase == _ReplayPhase.done) {
        if (mounted) {
          setState(() => _awaitingAutoStart = false);
        }
      } else if (phase == _ReplayPhase.error) {
        if (mounted) {
          setState(() => _awaitingAutoStart = false);
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
                content: Text(
                    'Replay entered ERROR state during MT5 fetch/import.')),
          );
        }
      }
    } catch (_) {
      // Keep waiting; transient polling failures should not cancel auto-start.
    } finally {
      _autoStartBusy = false;
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

  DateTime _toOffset(DateTime localTime, Duration offset) {
    return localTime.toUtc().add(offset);
  }

  Widget _buildTimezoneHint(BuildContext context) {
    final localFrom = _fromDate;
    final localTo = _toDate;
    final utcFrom = localFrom.toUtc();
    final utcTo = localTo.toUtc();
    final ksaFrom = _toOffset(localFrom, const Duration(hours: 3));
    final ksaTo = _toOffset(localTo, const Duration(hours: 3));
    final istFrom = _toOffset(localFrom, const Duration(hours: 5, minutes: 30));
    final istTo = _toOffset(localTo, const Duration(hours: 5, minutes: 30));

    final cs = Theme.of(context).colorScheme;
    final textTheme = Theme.of(context).textTheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: cs.secondaryContainer.withOpacity(0.35),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Selected Range Across Timezones',
              style: textTheme.titleSmall?.copyWith(color: cs.onSecondaryContainer)),
          const SizedBox(height: 8),
          Text('Local: ${_fmtDt(localFrom)} -> ${_fmtDt(localTo)}',
              style: textTheme.bodySmall),
          Text('UTC:   ${_fmtDt(utcFrom)} -> ${_fmtDt(utcTo)}',
              style: textTheme.bodySmall),
          Text('KSA:   ${_fmtDt(ksaFrom)} -> ${_fmtDt(ksaTo)}',
              style: textTheme.bodySmall),
          Text('IST:   ${_fmtDt(istFrom)} -> ${_fmtDt(istTo)}',
              style: textTheme.bodySmall),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final replayAsync = ref.watch(replayStatusProvider);
    final cs = Theme.of(context).colorScheme;

    return RefreshIndicator(
      onRefresh: _refresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // Status + progress
          replayAsync.when(
            data: (data) => _StatusCard(
              status: data.status,
              importedCandles: data.importedCandles,
              onPause: _pauseReplay,
              onResume: _resumeReplay,
              onStop: _stopReplay,
              pausing: _pausing,
              resuming: _resuming,
              stopping: _stopping,
            ),
            loading: () => const Card(
              child: Padding(
                  padding: EdgeInsets.all(16),
                  child: LinearProgressIndicator()),
            ),
            error: (e, _) => Card(
              child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Text('Status error: $e')),
            ),
          ),

          const SizedBox(height: 16),

          // Config card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Configuration',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 12),

                  TextField(
                    controller: _symbolController,
                    decoration: const InputDecoration(
                      labelText: 'Symbol',
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.show_chart),
                    ),
                  ),
                  const SizedBox(height: 12),

                  Row(
                    children: [
                      Expanded(
                          child: _DateButton(
                              label: 'From',
                              value: _fromDate,
                              onTap: _pickFromDate)),
                      const SizedBox(width: 8),
                      Expanded(
                          child: _DateButton(
                              label: 'To', value: _toDate, onTap: _pickToDate)),
                    ],
                  ),
                  const SizedBox(height: 10),
                  _buildTimezoneHint(context),
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
                          label: '${_speedMultiplier}x',
                          onChanged: (v) =>
                              setState(() => _speedMultiplier = v.round()),
                        ),
                      ),
                      Text('${_speedMultiplier}x'),
                    ],
                  ),

                  const SizedBox(height: 8),
                  TextField(
                    controller: _initialCashController,
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: false),
                    decoration: const InputDecoration(
                      labelText: 'Replay Initial Cash (AED)',
                      helperText:
                          'Replay-only capital; live trading account is unaffected.',
                      border: OutlineInputBorder(),
                      prefixIcon: Icon(Icons.account_balance_wallet_outlined),
                    ),
                  ),

                  SwitchListTile.adaptive(
                    contentPadding: EdgeInsets.zero,
                    value: _useMockAi,
                    onChanged: (v) => setState(() => _useMockAi = v),
                    title: const Text('Use Mock AI'),
                    subtitle: const Text(
                        'Fast replay. Disable only to validate real AI.'),
                  ),
                  SwitchListTile.adaptive(
                    contentPadding: EdgeInsets.zero,
                    value: _useLiveNewsAndTelegramInReplay,
                    onChanged: (v) =>
                        setState(() => _useLiveNewsAndTelegramInReplay = v),
                    title: const Text('Use live News/Telegram in replay'),
                    subtitle: const Text(
                        'Off by default. Turn on only to test scenarios with current news/telegram.'),
                  ),

                  const Divider(height: 24),

                  // One-click MT5 replay
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: cs.primaryContainer.withOpacity(0.4),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Icon(Icons.bolt, color: cs.primary, size: 20),
                            const SizedBox(width: 6),
                            Text('One-Click MT5 Replay',
                                style: Theme.of(context)
                                    .textTheme
                                    .titleSmall
                                    ?.copyWith(color: cs.primary)),
                          ],
                        ),
                        const SizedBox(height: 6),
                        const Text(
                          'Requests M5/M15/H1 candles from the MT5 EA, imports them, '
                          'and starts replay automatically.\nRequires the EA to be running.',
                          style: TextStyle(fontSize: 12),
                        ),
                        const SizedBox(height: 10),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton.icon(
                            onPressed: _running ? null : _runReplay,
                            icon: _running
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(
                                        strokeWidth: 2, color: Colors.white))
                                : const Icon(Icons.play_arrow),
                            label: Text(
                                _running ? 'Starting…' : 'Run Replay from MT5'),
                          ),
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(height: 12),

                  Text('Already imported data?',
                      style: Theme.of(context)
                          .textTheme
                          .labelSmall
                          ?.copyWith(color: cs.onSurfaceVariant)),
                  const SizedBox(height: 6),
                  OutlinedButton.icon(
                    onPressed: _running ? null : _startManualReplay,
                    icon: const Icon(Icons.play_arrow, size: 18),
                    label: const Text('Start with imported candles'),
                  ),
                ],
              ),
            ),
          ),

          const SizedBox(height: 16),

          // Timeline
          Text('Recent Timeline',
              style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          Consumer(builder: (context, ref, _) {
            final timeline = ref.watch(timelineProvider);
            return timeline.when(
              data: (items) {
                if (items.isEmpty) {
                  return const Card(
                    child: Padding(
                        padding: EdgeInsets.all(16),
                        child: Text('No timeline events yet.')),
                  );
                }
                return Column(
                  children: items.reversed.take(60).map((item) {
                    return Card(
                      margin: const EdgeInsets.only(bottom: 4),
                      child: ListTile(
                        dense: true,
                        title: Text('${item.eventType} · ${item.stage}'),
                        subtitle: Text('${_fmtDt(item.createdAtUtc)} · '
                            'cycle=${item.cycleId ?? '-'}'),
                      ),
                    );
                  }).toList(),
                );
              },
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => Text('Timeline error: $e'),
            );
          }),
        ],
      ),
    );
  }
}

String _fmtDt(DateTime dt) =>
    '${dt.year}-${dt.month.toString().padLeft(2, '0')}-'
    '${dt.day.toString().padLeft(2, '0')} '
    '${dt.hour.toString().padLeft(2, '0')}:'
    '${dt.minute.toString().padLeft(2, '0')}';

// ─── Status card ─────────────────────────────────────────────────────────────

class _StatusCard extends StatelessWidget {
  const _StatusCard({
    required this.status,
    required this.importedCandles,
    required this.onPause,
    required this.onResume,
    required this.onStop,
    required this.pausing,
    required this.resuming,
    required this.stopping,
  });

  final ReplayStatus status;
  final Map<String, int> importedCandles;
  final VoidCallback onPause;
  final VoidCallback onResume;
  final VoidCallback onStop;
  final bool pausing;
  final bool resuming;
  final bool stopping;

  @override
  Widget build(BuildContext context) {
    final phase = _parsePhase(status.phase);
    final cs = Theme.of(context).colorScheme;
    final overallProgress = _overallReplayProgress(phase, status);
    final progressPct =
        (overallProgress * 100).clamp(0, 100).toStringAsFixed(1);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text('Status', style: Theme.of(context).textTheme.titleMedium),
                const Spacer(),
                _PhaseChip(phase: phase),
              ],
            ),
            const SizedBox(height: 12),
            _ProgressSteps(phase: phase),
            const SizedBox(height: 12),
            LinearProgressIndicator(
              value: overallProgress,
              minHeight: 8,
              borderRadius: BorderRadius.circular(8),
            ),
            const SizedBox(height: 6),
            Text(
              'Overall progress: $progressPct%',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            if (status.isRunning) ...[
              const SizedBox(height: 12),
              LinearProgressIndicator(
                value: status.totalCandles > 0
                    ? status.processedCandles / status.totalCandles
                    : null,
              ),
              const SizedBox(height: 4),
              Text(
                '${status.processedCandles} / ${status.totalCandles} candles',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
            const SizedBox(height: 12),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                if (status.symbol.isNotEmpty)
                  _chip(context, 'Symbol', status.symbol),
                _chip(context, 'Cycles', '${status.cyclesTriggered}'),
                _chip(context, 'Setups', '${status.setupCandidatesFound}'),
                _chip(context, 'Trades', '${status.tradesArmed}'),
                if (status.driverTimeframe.isNotEmpty)
                  _chip(context, 'Driver TF', status.driverTimeframe),
                for (final e in importedCandles.entries)
                  _chip(context, e.key, '${e.value}'),
              ],
            ),
            if (status.isRunning) ...[
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  if (!status.isPaused)
                    OutlinedButton.icon(
                      onPressed: pausing ? null : onPause,
                      icon: const Icon(Icons.pause, size: 18),
                      label: Text(pausing ? 'Pausing…' : 'Pause'),
                    )
                  else
                    FilledButton.icon(
                      onPressed: resuming ? null : onResume,
                      icon: const Icon(Icons.play_arrow, size: 18),
                      label: Text(resuming ? 'Resuming…' : 'Resume'),
                    ),
                  OutlinedButton.icon(
                    onPressed: stopping ? null : onStop,
                    style: OutlinedButton.styleFrom(foregroundColor: cs.error),
                    icon: const Icon(Icons.stop, size: 18),
                    label: Text(stopping ? 'Stopping…' : 'Stop'),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _chip(BuildContext context, String label, String value) => Chip(
        label: Text('$label: $value'),
        labelStyle: Theme.of(context).textTheme.labelSmall,
      );
}

double _overallReplayProgress(_ReplayPhase phase, ReplayStatus status) {
  final replayProgress = status.totalCandles > 0
      ? (status.processedCandles / status.totalCandles).clamp(0.0, 1.0)
      : 0.0;

  return switch (phase) {
    _ReplayPhase.idle => 0.0,
    _ReplayPhase.mt5FetchQueued => 0.15,
    _ReplayPhase.mt5FetchReceived => 0.45,
    _ReplayPhase.importing => 0.65,
    _ReplayPhase.running => 0.65 + (replayProgress * 0.35),
    _ReplayPhase.paused => 0.65 + (replayProgress * 0.35),
    _ReplayPhase.done => 1.0,
    _ReplayPhase.error => replayProgress > 0 ? replayProgress : 0.1,
  };
}

// ─── Progress steps ───────────────────────────────────────────────────────────

class _ProgressSteps extends StatelessWidget {
  const _ProgressSteps({required this.phase});

  final _ReplayPhase phase;

  @override
  Widget build(BuildContext context) {
    final bool isError = phase == _ReplayPhase.error;
    final steps = [
      (
        label: 'Fetch MT5',
        done: phase.index > _ReplayPhase.mt5FetchQueued.index && !isError,
        active: phase == _ReplayPhase.mt5FetchQueued,
      ),
      (
        label: 'Import',
        done: phase.index > _ReplayPhase.importing.index && !isError,
        active: phase == _ReplayPhase.mt5FetchReceived ||
            phase == _ReplayPhase.importing,
      ),
      (
        label: 'Replay',
        done: phase == _ReplayPhase.done,
        active: phase == _ReplayPhase.running || phase == _ReplayPhase.paused,
      ),
    ];

    return Row(
      children: steps
          .expand((s) => [
                _StepIcon(
                    label: s.label,
                    done: s.done,
                    active: s.active,
                    error: isError),
                if (s != steps.last)
                  const Expanded(child: Divider(thickness: 1.5)),
              ])
          .toList(),
    );
  }
}

class _StepIcon extends StatelessWidget {
  const _StepIcon({
    required this.label,
    required this.done,
    required this.active,
    required this.error,
  });

  final String label;
  final bool done;
  final bool active;
  final bool error;

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final color = error
        ? cs.error
        : done
            ? Colors.green
            : active
                ? cs.primary
                : cs.onSurfaceVariant.withOpacity(0.4);

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 30,
          height: 30,
          decoration: BoxDecoration(
            color: color.withOpacity(0.15),
            shape: BoxShape.circle,
            border: Border.all(color: color, width: 2),
          ),
          child: Center(
            child: active && !done
                ? SizedBox(
                    width: 14,
                    height: 14,
                    child:
                        CircularProgressIndicator(strokeWidth: 2, color: color))
                : done
                    ? Icon(Icons.check, size: 14, color: color)
                    : error
                        ? Icon(Icons.close, size: 14, color: color)
                        : Icon(Icons.circle_outlined, size: 14, color: color),
          ),
        ),
        const SizedBox(height: 3),
        Text(label,
            style: Theme.of(context)
                .textTheme
                .labelSmall
                ?.copyWith(color: color, fontSize: 9)),
      ],
    );
  }
}

// ─── Phase chip ───────────────────────────────────────────────────────────────

class _PhaseChip extends StatelessWidget {
  const _PhaseChip({required this.phase});

  final _ReplayPhase phase;

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (phase) {
      _ReplayPhase.idle => ('Idle', Colors.grey),
      _ReplayPhase.mt5FetchQueued => ('Fetching…', Colors.orange),
      _ReplayPhase.mt5FetchReceived => ('Received', Colors.blue),
      _ReplayPhase.importing => ('Importing…', Colors.blue),
      _ReplayPhase.running => ('Running', Colors.green),
      _ReplayPhase.paused => ('Paused', Colors.orange),
      _ReplayPhase.done => ('Done ✓', Colors.green),
      _ReplayPhase.error => ('Error', Colors.red),
    };

    return Chip(
      label: Text(label),
      labelStyle:
          TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 12),
      backgroundColor: color.withOpacity(0.12),
      side: BorderSide(color: color.withOpacity(0.4)),
    );
  }
}

// ─── Date button ──────────────────────────────────────────────────────────────

class _DateButton extends StatelessWidget {
  const _DateButton(
      {required this.label, required this.value, required this.onTap});

  final String label;
  final DateTime value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(8),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        decoration: BoxDecoration(
          border: Border.all(
              color: Theme.of(context).colorScheme.outline.withOpacity(0.6)),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label,
                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: Theme.of(context).colorScheme.onSurfaceVariant)),
            const SizedBox(height: 2),
            Row(
              children: [
                const Icon(Icons.calendar_today, size: 14),
                const SizedBox(width: 4),
                Text(
                  _fmtDt(value),
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
