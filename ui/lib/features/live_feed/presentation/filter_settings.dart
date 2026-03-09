import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../domain/models.dart';

// date range selection that was previously defined privately in live_feed_screen
enum FeedDateFilter { today, lastWeek }

extension FeedDateFilterLabel on FeedDateFilter {
  String label(DateTime nowUtc) {
    final ksaNow = nowUtc.add(const Duration(hours: 3));
    switch (this) {
      case FeedDateFilter.today:
        final m = ksaNow.month.toString().padLeft(2, '0');
        final d = ksaNow.day.toString().padLeft(2, '0');
        return 'Today (${ksaNow.year}-$m-$d)';
      case FeedDateFilter.lastWeek:
        final weekEnd = ksaNow.subtract(const Duration(days: 1));
        final weekStart = ksaNow.subtract(const Duration(days: 7));
        final sm = weekStart.month.toString().padLeft(2, '0');
        final sd = weekStart.day.toString().padLeft(2, '0');
        final em = weekEnd.month.toString().padLeft(2, '0');
        final ed = weekEnd.day.toString().padLeft(2, '0');
        return 'Last Week ($sm-$sd – $em-$ed)';
    }
  }
}

/// Settings that control how the live-feed timeline is filtered.
///
/// The notifier exposes helpers for updating the date range or session set.
class LiveFeedFilterSettings {
  LiveFeedFilterSettings({
    required this.dateFilter,
    Set<String>? sessions,
  }) : sessions = sessions ?? <String>{};

  FeedDateFilter dateFilter;

  /// empty means "no session filtering" (show all sessions)
  Set<String> sessions;

  /// returns true if the supplied event passes both date and session filters
  bool matches(RuntimeTimelineItem item) {
    final ksaItemTime = item.createdAtUtc.add(const Duration(hours: 3));
    final ksaNow = DateTime.now().toUtc().add(const Duration(hours: 3));

    // date portion
    final datePass = switch (dateFilter) {
      FeedDateFilter.today => ksaItemTime.year == ksaNow.year &&
          ksaItemTime.month == ksaNow.month &&
          ksaItemTime.day == ksaNow.day,
      FeedDateFilter.lastWeek => !ksaItemTime.isBefore(
              DateTime(ksaNow.year, ksaNow.month, ksaNow.day)
                  .subtract(const Duration(days: 7))) &&
          ksaItemTime.isBefore(DateTime(ksaNow.year, ksaNow.month, ksaNow.day)),
    };

    if (!datePass) return false;

    // session portion
    if (sessions.isEmpty) return true;
    final sess = item.payload['session']?.toString().toUpperCase() ?? '';
    return sessions.contains(sess);
  }

  LiveFeedFilterSettings copy() => LiveFeedFilterSettings(
        dateFilter: dateFilter,
        sessions: Set.from(sessions),
      );
}

class _LiveFeedFilterNotifier extends StateNotifier<LiveFeedFilterSettings> {
  _LiveFeedFilterNotifier()
      : super(LiveFeedFilterSettings(dateFilter: FeedDateFilter.today));

  void setDateFilter(FeedDateFilter filter) {
    state = state.copy()..dateFilter = filter;
  }

  void toggleSession(String session) {
    final newState = state.copy();
    final upper = session.toUpperCase();
    if (newState.sessions.contains(upper)) {
      newState.sessions.remove(upper);
    } else {
      newState.sessions.add(upper);
    }
    state = newState;
  }

  void clearSessions() {
    state = state.copy()..sessions.clear();
  }
}

final liveFeedFilterProvider =
    StateNotifierProvider<_LiveFeedFilterNotifier, LiveFeedFilterSettings>(
  (ref) => _LiveFeedFilterNotifier(),
);
