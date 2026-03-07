import 'package:flutter/material.dart';

import '../../dashboard/presentation/dashboard_screen.dart';
import '../../ledger/presentation/ledger_screen.dart';
import '../../replay/presentation/replay_screen.dart';
import '../../risk/presentation/risk_control_screen.dart';
import '../../sessions/presentation/session_overview_screen.dart';
import '../../strategies/presentation/strategy_control_screen.dart';

/// Secondary navigation hub — shows cards that lead to less-frequently-accessed
/// features.  Tapping a card pushes the target screen as a full-page route.
class MoreScreen extends StatelessWidget {
  const MoreScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final items = [
      _MoreItem(
        icon: Icons.dashboard,
        label: 'Dashboard',
        subtitle: 'KPIs, P&L and system overview',
        color: Colors.blue.shade600,
        builder: (_) => const DashboardScreen(isEmergencyPaused: false),
      ),
      _MoreItem(
        icon: Icons.schedule,
        label: 'Sessions',
        subtitle: 'Enable or disable trading sessions',
        color: Colors.teal.shade600,
        builder: (_) => const SessionOverviewScreen(),
      ),
      _MoreItem(
        icon: Icons.account_balance,
        label: 'Ledger',
        subtitle: 'Deposits, withdrawals and cash buckets',
        color: Colors.indigo.shade600,
        builder: (_) => const LedgerScreen(),
      ),
      _MoreItem(
        icon: Icons.replay,
        label: 'Replay',
        subtitle: 'Run historical data through the engine',
        color: Colors.orange.shade700,
        builder: (_) => const ReplayScreen(),
      ),
      _MoreItem(
        icon: Icons.shield,
        label: 'Risk',
        subtitle: 'Risk profiles and control settings',
        color: Colors.red.shade600,
        builder: (_) => const RiskControlScreen(),
      ),
      _MoreItem(
        icon: Icons.tune,
        label: 'Strategies',
        subtitle: 'Activate and manage strategy profiles',
        color: Colors.purple.shade600,
        builder: (_) => const StrategyControlScreen(),
      ),
    ];

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          'More',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 4),
        Text(
          'Additional tools and settings',
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
        ),
        const SizedBox(height: 16),
        for (final item in items)
          Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: Card(
              clipBehavior: Clip.antiAlias,
              child: InkWell(
                onTap: () => Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (routeContext) => _MoreRoutePage(
                      title: item.label,
                      child: item.builder(routeContext),
                    ),
                    settings: RouteSettings(name: item.label),
                  ),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Row(
                    children: [
                      Container(
                        width: 44,
                        height: 44,
                        decoration: BoxDecoration(
                          color: item.color.withOpacity(0.15),
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Icon(item.icon, color: item.color, size: 24),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              item.label,
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.copyWith(fontWeight: FontWeight.w600),
                            ),
                            Text(
                              item.subtitle,
                              style: Theme.of(context)
                                  .textTheme
                                  .bodySmall
                                  ?.copyWith(
                                    color: Theme.of(context)
                                        .colorScheme
                                        .onSurfaceVariant,
                                  ),
                            ),
                          ],
                        ),
                      ),
                      Icon(
                        Icons.chevron_right,
                        color: Theme.of(context)
                            .colorScheme
                            .onSurfaceVariant
                            .withOpacity(0.5),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class _MoreItem {
  const _MoreItem({
    required this.icon,
    required this.label,
    required this.subtitle,
    required this.color,
    required this.builder,
  });

  final IconData icon;
  final String label;
  final String subtitle;
  final Color color;
  final WidgetBuilder builder;
}

class _MoreRoutePage extends StatelessWidget {
  const _MoreRoutePage({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: SafeArea(child: child),
    );
  }
}
