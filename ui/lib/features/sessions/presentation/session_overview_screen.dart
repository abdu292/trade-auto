import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

class SessionOverviewScreen extends ConsumerWidget {
  const SessionOverviewScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sessions = ref.watch(sessionsProvider);

    Future<void> toggle(String session, bool isEnabled) async {
      final messenger = ScaffoldMessenger.of(context);
      try {
        await ref.read(brainApiProvider).toggleSession(session, isEnabled);
        ref.invalidate(sessionsProvider);
      } catch (error) {
        messenger.showSnackBar(
            SnackBar(content: Text('Failed to update session: $error')));
      }
    }

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(sessionsProvider),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text('Session Controls',
              style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 12),
          sessions.when(
            data: (items) {
              if (items.isEmpty) {
                return const Card(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No session records available.'),
                  ),
                );
              }
              return Column(
                children: items
                    .map(
                      (item) => Card(
                        child: SwitchListTile(
                          value: item.isEnabled,
                          title: Text(item.session),
                          subtitle:
                              Text('Updated: ${item.updatedAtUtc.toLocal()}'),
                          onChanged: (value) => toggle(item.session, value),
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
                child: Text('Error loading sessions: $error'),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
