import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/models.dart';
import '../../../presentation/app_providers.dart';

class TradesScreen extends ConsumerStatefulWidget {
  const TradesScreen({super.key});

  @override
  ConsumerState<TradesScreen> createState() => _TradesScreenState();
}

class _TradesScreenState extends ConsumerState<TradesScreen> {
  final _symbolController = TextEditingController(text: 'XAUUSD');
  final _priceController = TextEditingController(text: '3350');
  String _session = 'London';
  bool _submitting = false;

  @override
  void dispose() {
    _symbolController.dispose();
    _priceController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final trades = ref.watch(activeTradesProvider);
    final signals = ref.watch(signalsProvider);

    Future<void> refresh() async {
      ref
        ..invalidate(activeTradesProvider)
        ..invalidate(signalsProvider)
        ..invalidate(approvalsProvider)
        ..invalidate(notificationsProvider);
    }

    Future<void> analyzeNow() async {
      final messenger = ScaffoldMessenger.of(context);
      final price = double.tryParse(_priceController.text.trim());
      if (price == null || price <= 0) {
        messenger.showSnackBar(
            const SnackBar(content: Text('Enter a valid price.')));
        return;
      }

      setState(() => _submitting = true);
      try {
        final input = AnalyzeSnapshotInput(
          symbol: _symbolController.text.trim().toUpperCase(),
          session: _session,
          price: price,
        );
        await ref.read(brainApiProvider).analyzeSnapshot(input);
        await refresh();
      } catch (error) {
        messenger
            .showSnackBar(SnackBar(content: Text('Analyze failed: $error')));
      } finally {
        if (mounted) {
          setState(() => _submitting = false);
        }
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
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Quick Analyze',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _symbolController,
                    textCapitalization: TextCapitalization.characters,
                    decoration: const InputDecoration(
                        labelText: 'Symbol', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: _priceController,
                    keyboardType:
                        const TextInputType.numberWithOptions(decimal: true),
                    decoration: const InputDecoration(
                        labelText: 'Current Price',
                        border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 10),
                  DropdownButtonFormField<String>(
                    initialValue: _session,
                    decoration: const InputDecoration(
                        labelText: 'Session', border: OutlineInputBorder()),
                    items: const ['Asia', 'London', 'NewYork']
                        .map((value) => DropdownMenuItem<String>(
                            value: value, child: Text(value)))
                        .toList(),
                    onChanged: (value) {
                      if (value != null) {
                        setState(() => _session = value);
                      }
                    },
                  ),
                  const SizedBox(height: 12),
                  FilledButton.icon(
                    onPressed: _submitting ? null : analyzeNow,
                    icon: _submitting
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.analytics),
                    label: const Text('Analyze Snapshot'),
                  ),
                ],
              ),
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
