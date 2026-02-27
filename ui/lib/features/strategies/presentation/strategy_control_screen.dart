import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class StrategyControlScreen extends ConsumerWidget {
  const StrategyControlScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final strategies = ref.watch(strategiesProvider);

    Future<void> activate(String id) async {
      final messenger = ScaffoldMessenger.of(context);
      try {
        await ref.read(brainApiProvider).activateStrategy(id);
        ref.invalidate(strategiesProvider);
      } catch (error) {
        messenger.showSnackBar(
            SnackBar(content: Text('Failed to activate strategy: $error')));
      }
    }

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(strategiesProvider),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Strategy Profiles',
              style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          strategies.when(
            data: (items) {
              if (items.isEmpty) {
                return const Card(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No strategy profiles available.'),
                  ),
                );
              }

              return Column(
                children: items
                    .map(
                      (item) => Card(
                        child: ListTile(
                          title: Text(item.name),
                          subtitle: Text(item.description),
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
                child: Text('Error loading strategies: $error'),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
