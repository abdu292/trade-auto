import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class RiskControlScreen extends ConsumerWidget {
  const RiskControlScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final riskProfiles = ref.watch(riskProfilesProvider);

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
      onRefresh: () async => ref.invalidate(riskProfilesProvider),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
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
        ],
      ),
    );
  }
}
