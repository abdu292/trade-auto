import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';
import 'filter_settings.dart';

/// Dialog shown when the user taps the filter icon on the app bar.
///
/// It reads the current filter settings and lets the user update them.
class FilterDialog extends ConsumerWidget {
  const FilterDialog({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settings = ref.watch(liveFeedFilterProvider);
    final timeline = ref.watch(timelineProvider);
    final runtime = ref.watch(runtimeStatusProvider);

    return AlertDialog(
      title: const Text('Filters'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Date range'),
          const SizedBox(height: 8),
          for (final option in FeedDateFilter.values)
            RadioListTile<FeedDateFilter>(
              value: option,
              groupValue: settings.dateFilter,
              title: Text(option.label(DateTime.now().toUtc())),
              onChanged: (v) {
                if (v != null) {
                  ref.read(liveFeedFilterProvider.notifier).setDateFilter(v);
                }
              },
            ),
          const Divider(),
          const Text('Sessions'),
          const SizedBox(height: 8),
          timeline.when(
            data: (items) {
              // compute unique session strings from payload
              final sess = <String>{};
              for (final item in items) {
                final s = item.payload['session']?.toString().toUpperCase();
                if (s != null && s.isNotEmpty) sess.add(s);
              }
              if (sess.isEmpty) {
                return const Text('No session data available');
              }
              final sorted = sess.toList()..sort();

              // determine current session from runtime status
              final curr = runtime.maybeWhen(
                  data: (rs) => rs.session.toUpperCase(), orElse: () => null);

              return Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  CheckboxListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Show all sessions'),
                    value: settings.sessions.isEmpty,
                    onChanged: (v) {
                      if (v == true) {
                        ref
                            .read(liveFeedFilterProvider.notifier)
                            .clearSessions();
                      }
                    },
                  ),
                  // note: the "only current session" checkbox was removed per UX feedback.
                  // we now mark the current session in the list below instead.
                  for (final s in sorted)
                    CheckboxListTile(
                      contentPadding: EdgeInsets.zero,
                      title: Text((curr != null && curr.isNotEmpty && s == curr)
                          ? '$s (current session)'
                          : s),
                      value: settings.sessions.contains(s),
                      onChanged: (v) {
                        ref
                            .read(liveFeedFilterProvider.notifier)
                            .toggleSession(s);
                      },
                    ),
                ],
              );
            },
            loading: () => const SizedBox(
                height: 50, child: Center(child: CircularProgressIndicator())),
            error: (e, _) => Text('Error loading sessions: $e'),
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Close'),
        ),
      ],
    );
  }
}
