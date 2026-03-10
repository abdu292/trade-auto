import 'package:flutter/material.dart';

/// Read-only screen that explains what the MT5 Expert Advisor (EA) currently
/// does inside the trading system.  Every section corresponds to a distinct
/// responsibility of the EA so that any team member can quickly understand the
/// live execution layer.
class EaInfoScreen extends StatelessWidget {
  const EaInfoScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
      children: const [
        _IntroCard(),
        SizedBox(height: 12),
        _SectionCard(
          icon: Icons.bar_chart_outlined,
          iconColor: Color(0xFF00796B),
          title: 'Market Monitoring',
          body:
              'The EA attaches to the XAUUSD chart inside MetaTrader 5 and '
              'listens to every live tick. On each M5, M15, and H1 candle '
              'close it assembles a rich market snapshot that includes '
              'multi-timeframe OHLCV data, rolling spread statistics '
              '(1-minute and 5-minute windows over a 60-sample buffer), '
              'session VWAP, and full account context (free margin, equity, '
              'balance). This snapshot is immediately posted to the Brain '
              'backend so the decision engine always has current market state.',
        ),
        SizedBox(height: 12),
        _SectionCard(
          icon: Icons.send_outlined,
          iconColor: Color(0xFF1565C0),
          title: 'Snapshot Push',
          body:
              'Every SnapshotPushSeconds (default 30 s) — and on every '
              'qualifying candle close — the EA serialises the snapshot '
              'and calls POST /mt5/snapshot on the Brain API. The payload '
              'includes the open pending orders and open positions for the '
              'current symbol so the engine can enforce per-symbol capital '
              'caps and waterfall risk checks in real time.',
        ),
        SizedBox(height: 12),
        _SectionCard(
          icon: Icons.sync_outlined,
          iconColor: Color(0xFF6A1B9A),
          title: 'Trade Polling',
          body:
              'Every PollTradeSeconds (default 2 s) the EA calls GET '
              '/mt5/pending-trades. Brain responds with a queued '
              'TradeCommand (BUY_LIMIT or BUY_STOP) whenever the decision '
              'engine has approved a new entry. The EA immediately hands '
              'each command to the local risk guards before touching the '
              'broker.',
        ),
        SizedBox(height: 12),
        _SectionCard(
          icon: Icons.check_circle_outline,
          iconColor: Color(0xFF2E7D32),
          title: 'Trade Execution',
          body:
              'Before placing any order the EA runs a series of RiskGuard '
              'checks:\n'
              '• Price and TP must be greater than zero\n'
              '• Minimum order size ≥ 100 g (configurable via Brain)\n'
              '• Order type must be BUY_LIMIT or BUY_STOP (no market '
              'orders, no shorts)\n'
              '• BUY_STOP is blocked when waterfall risk = HIGH\n'
              '• Execution is blocked entirely when engine state = '
              'CAPITAL_PROTECTED\n\n'
              'On success, gram quantity is converted to MT5 lots '
              '(1 lot = 100 g, clamped to 0.01 – 5.0 lots) and the pending '
              'order is placed on the broker. The EA then posts the '
              'resulting ticket and status back to Brain via '
              'POST /mt5/trade-status.',
        ),
        SizedBox(height: 12),
        _SectionCard(
          icon: Icons.notifications_active_outlined,
          iconColor: Color(0xFFEF6C00),
          title: 'Trade Tracking',
          body:
              'The EA implements OnTradeTransaction to react to broker '
              'events in real time:\n'
              '• BUY_TRIGGERED — fired when a pending order is filled at '
              'entry. The EA reports ticket, fill price, grams, and '
              'timestamp to Brain.\n'
              '• TP_HIT — fired when the take-profit level is reached and '
              'the position closes. Brain records the realised profit and '
              'updates the physical-gold ledger accordingly.',
        ),
        SizedBox(height: 12),
        _SectionCard(
          icon: Icons.cancel_outlined,
          iconColor: Color(0xFFC62828),
          title: 'Control Signals',
          body:
              'Alongside trade commands, the EA polls for two control '
              'signals from Brain:\n'
              '• Cancel Pending Orders — instructs the EA to close all '
              'open pending orders immediately (kill-switch / panic mode).\n'
              '• Fetch History Request — triggers a history dump so Brain '
              'can run a historical replay session without restarting the '
              'EA.',
        ),
        SizedBox(height: 12),
        _SectionCard(
          icon: Icons.settings_ethernet,
          iconColor: Color(0xFF37474F),
          title: 'Key Configuration',
          body:
              'BrainBaseUrl — Base URL of the Brain API '
              '(default: http://127.0.0.1:5000)\n'
              'BrainApiKey — API key for authenticating requests '
              '(default: dev-local-change-me)\n'
              'SnapshotPushSeconds — How often the EA pushes a full market '
              'snapshot (default: 30 s)\n'
              'PollTradeSeconds — How often the EA polls for new trade '
              'commands (default: 2 s)',
        ),
      ],
    );
  }
}

class _IntroCard extends StatelessWidget {
  const _IntroCard();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Card(
      color: colorScheme.primaryContainer,
      elevation: 0,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.smart_toy_outlined,
              size: 36,
              color: colorScheme.onPrimaryContainer,
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'MT5 Expert Advisor',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: colorScheme.onPrimaryContainer,
                          fontWeight: FontWeight.w700,
                        ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'The EA is the live execution layer that runs inside '
                    'MetaTrader 5. It watches XAUUSD, pushes real-time market '
                    'snapshots to Brain, receives approved trade commands, '
                    'applies local risk guards, executes orders on the broker, '
                    'and reports fills back — forming a closed feedback loop '
                    'between the decision engine and the market.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: colorScheme.onPrimaryContainer,
                        ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({
    required this.icon,
    required this.iconColor,
    required this.title,
    required this.body,
  });

  final IconData icon;
  final Color iconColor;
  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: iconColor.withOpacity(0.12),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Icon(icon, color: iconColor, size: 22),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    title,
                    style: Theme.of(context)
                        .textTheme
                        .titleMedium
                        ?.copyWith(fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              body,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                    height: 1.5,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}
