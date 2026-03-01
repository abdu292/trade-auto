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
        messenger.showSnackBar(
          const SnackBar(content: Text('Strategy profile updated.')),
        );
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

              final activeItems = items.where((item) => item.isActive).toList();
              final active = activeItems.isEmpty ? null : activeItems.first;
              final quickSwitchItems = items
                  .where(
                    (item) =>
                        item.name.toLowerCase() == 'standard' ||
                        item.name.toLowerCase() == 'warpremium',
                  )
                  .toList();

              final children = <Widget>[
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Quick Mode Switch',
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        const SizedBox(height: 8),
                        Text(
                          active == null
                              ? 'No active profile'
                              : 'Active: ${active.name}',
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: quickSwitchItems
                              .map(
                                (item) => FilledButton.tonal(
                                  onPressed: item.isActive
                                      ? null
                                      : () => activate(item.id),
                                  child: Text(
                                    item.isActive
                                        ? '${item.name} (Active)'
                                        : item.name,
                                  ),
                                ),
                              )
                              .toList(),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 12),
              ];

              children.addAll(
                items.map(
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
                ),
              );

              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: children,
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
