import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../../../core/network/api_client.dart';
import '../../../presentation/app_providers.dart';
import '../../../domain/models.dart';

/// Settings page containing environment, emergency pause and trading toggles.
class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({super.key});

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  bool _isTogglingAutoTrade = false;
  bool _isUpdatingMinGrams = false;
  bool _isTogglingMicroRotation = false;
  late final Future<PackageInfo> _packageInfoFuture;

  @override
  void initState() {
    super.initState();
    _packageInfoFuture = PackageInfo.fromPlatform();
  }

  Future<void> _openEnvironmentDialog(BuildContext context) async {
    final selected = ref.read(selectedApiEnvironmentProvider);
    final effectiveBase = ref.read(effectiveApiBaseUrlProvider);

    ApiEnvironment tempEnvironment = selected;

    final saved = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            return AlertDialog(
              title: const Text('API Environment'),
              content: SizedBox(
                width: 520,
                child: SingleChildScrollView(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      DropdownButtonFormField<ApiEnvironment>(
                        initialValue: tempEnvironment,
                        decoration: const InputDecoration(
                          labelText: 'Environment',
                        ),
                        items: const [
                          DropdownMenuItem(
                            value: ApiEnvironment.production,
                            child: Text('Production (default)'),
                          ),
                          DropdownMenuItem(
                            value: ApiEnvironment.local,
                            child: Text('Local'),
                          ),
                        ],
                        onChanged: (value) {
                          if (value == null) return;
                          setDialogState(() => tempEnvironment = value);
                        },
                      ),
                      const SizedBox(height: 12),
                      Text(
                        'Current effective URL: $effectiveBase',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(false),
                  child: const Text('Cancel'),
                ),
                FilledButton(
                  onPressed: () => Navigator.of(dialogContext).pop(true),
                  child: const Text('Save'),
                ),
              ],
            );
          },
        );
      },
    );

    if (saved == true) {
      await ref
          .read(selectedApiEnvironmentProvider.notifier)
          .setEnvironment(tempEnvironment);
      // nothing else to invalidate since change will be applied when next API call occurs
    }
  }

  Future<void> _toggleAutoTrade(bool currentValue) async {
    if (_isTogglingAutoTrade) return;
    final newValue = !currentValue;
    final messenger = ScaffoldMessenger.of(context);

    // require confirmation when enabling
    if (newValue) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Enable Auto Trade?'),
          content: const Text(
            'When Auto Trade is ON, ARMED trades are routed directly to MT5 for automatic execution without manual approval — as long as all core laws pass.\n\n'
            'Make sure you are comfortable with the current settings before enabling this.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Enable Auto Trade'),
            ),
          ],
        ),
      );
      if (confirmed != true) return;
    }

    setState(() => _isTogglingAutoTrade = true);
    try {
      await ref.read(brainApiProvider).setAutoTradeEnabled(newValue);
      ref.invalidate(runtimeSettingsProvider);
      messenger.showSnackBar(
        SnackBar(
          content: Text(newValue
              ? '✅ Auto Trade ENABLED — trades will be sent to MT5 automatically.'
              : '⏸ Auto Trade DISABLED — trades will go to approval queue.'),
          duration: const Duration(seconds: 4),
        ),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to toggle Auto Trade: $error')),
      );
    } finally {
      if (mounted) setState(() => _isTogglingAutoTrade = false);
    }
  }

  Future<void> _updateMinTradeGrams(double currentValue) async {
    if (_isUpdatingMinGrams) return;
    final controller = TextEditingController(
      text: currentValue % 1 == 0
          ? currentValue.toStringAsFixed(0)
          : currentValue.toStringAsFixed(2),
    );
    final messenger = ScaffoldMessenger.of(context);

    final result = await showDialog<double>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Set Min Trade Grams'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Set the minimum trade size in grams. Orders below this threshold are rejected by the decision engine.\n\nDefault: 0.1 g.',
              style: TextStyle(fontSize: 13),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: controller,
              keyboardType:
                  const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(
                labelText: 'Min Grams',
                hintText: 'e.g. 50 or 0.5',
                border: OutlineInputBorder(),
                suffixText: 'g',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              final v = double.tryParse(controller.text.trim());
              if (v != null && v > 0) {
                Navigator.pop(ctx, v);
              }
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );

    controller.dispose();
    if (result == null || !mounted) return;

    setState(() => _isUpdatingMinGrams = true);
    try {
      await ref.read(brainApiProvider).setMinTradeGrams(result);
      ref.invalidate(runtimeSettingsProvider);
      messenger.showSnackBar(
        SnackBar(
            content: Text(
                'Min trade grams updated to ${result % 1 == 0 ? result.toStringAsFixed(0) : result.toStringAsFixed(2)} g.')),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to update min trade grams: $error')),
      );
    } finally {
      if (mounted) setState(() => _isUpdatingMinGrams = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final runtimeSettings = ref.watch(runtimeSettingsProvider);
    final emergencyPaused = ref.watch(emergencyPauseProvider);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Settings', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 12),
        // Environment switcher
        ListTile(
          leading: const Icon(Icons.cloud_sync_outlined),
          title: const Text('API Environment'),
          subtitle: Text(ref.watch(selectedApiEnvironmentProvider) ==
                  ApiEnvironment.production
              ? 'Production'
              : 'Local'),
          trailing: const Icon(Icons.arrow_forward_ios, size: 16),
          onTap: () => _openEnvironmentDialog(context),
        ),
        const Divider(),

        // Emergency pause toggle
        runtimeSettings.when(
          data: (settings) {
            final autoEnabled = settings.autoTradeEnabled;
            return SwitchListTile.adaptive(
              title: const Text('Emergency Pause'),
              subtitle: const Text('Stops manual actions when active'),
              value: emergencyPaused,
              onChanged: autoEnabled
                  ? (v) => ref.read(emergencyPauseProvider.notifier).state = v
                  : null,
            );
          },
          loading: () => const LinearProgressIndicator(),
          error: (_, __) => const SizedBox.shrink(),
        ),
        const Divider(),

        // Auto trade card
        _buildAutoTradeCard(runtimeSettings),
        const SizedBox(height: 12),
        // Min grams card
        _buildMinGramsCard(runtimeSettings),
        const SizedBox(height: 12),
        // Micro Rotation Mode card
        _buildMicroRotationCard(runtimeSettings),
        const SizedBox(height: 12),
        _buildVersionTile(),
        const SizedBox(height: 12),
      ],
    );
  }

  Widget _buildVersionTile() {
    return FutureBuilder<PackageInfo>(
      future: _packageInfoFuture,
      builder: (context, snapshot) {
        final subtitle = snapshot.hasData
            ? 'Version ${snapshot.data!.version} (${snapshot.data!.buildNumber})'
            : snapshot.hasError
                ? 'Version unavailable'
                : 'Loading app version...';
        return ListTile(
          leading: const Icon(Icons.info_outline),
          title: const Text('App Version'),
          subtitle: Text(subtitle),
        );
      },
    );
  }

  Widget _buildAutoTradeCard(AsyncValue<RuntimeSettings> runtimeSettings) {
    final cs = Theme.of(context).colorScheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: runtimeSettings.when(
          data: (settings) {
            final enabled = settings.autoTradeEnabled;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'When ON, ARMED trades are routed directly to MT5 for automatic execution without manual approval — as long as all core laws pass.\n\n'
                  'Default: OFF. Enable only when you are comfortable with the current settings.',
                  style: TextStyle(fontSize: 13),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Auto Trade',
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.copyWith(fontWeight: FontWeight.bold)),
                          const SizedBox(height: 6),
                          Text(
                            enabled
                                ? '✅ ON — trades route to MT5'
                                : '⏸ OFF — trades require approval',
                            style: TextStyle(
                                color: enabled ? Colors.green.shade700 : null),
                          ),
                        ],
                      ),
                    ),
                    if (_isTogglingAutoTrade)
                      const SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    else
                      Switch(
                        value: enabled,
                        onChanged: (_) => _toggleAutoTrade(enabled),
                        activeThumbColor: Colors.green.shade600,
                      ),
                  ],
                ),
              ],
            );
          },
          loading: () => const LinearProgressIndicator(),
          error: (e, _) => Text('Error loading settings: $e',
              style: TextStyle(color: cs.error)),
        ),
      ),
    );
  }

  Widget _buildMinGramsCard(AsyncValue<RuntimeSettings> runtimeSettings) {
    final cs = Theme.of(context).colorScheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: runtimeSettings.when(
          data: (settings) {
            final grams = settings.minTradeGrams;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Minimum gram quantity for any trade. Orders calculated below this threshold are rejected automatically.\n\nDefault: 100 g – lower to experiment with smaller positions.',
                  style: TextStyle(fontSize: 13),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'Min trade grams: ${grams % 1 == 0 ? grams.toStringAsFixed(0) : grams.toStringAsFixed(2)} g',
                        style: Theme.of(context)
                            .textTheme
                            .titleMedium
                            ?.copyWith(fontWeight: FontWeight.bold),
                      ),
                    ),
                    if (_isUpdatingMinGrams)
                      const SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    else
                      OutlinedButton.icon(
                        onPressed: () => _updateMinTradeGrams(grams),
                        icon: const Icon(Icons.edit, size: 16),
                        label: const Text('Change'),
                      ),
                  ],
                ),
              ],
            );
          },
          loading: () => const LinearProgressIndicator(),
          error: (e, _) => Text('Error loading settings: $e',
              style: TextStyle(color: cs.error)),
        ),
      ),
    );
  }
  Future<void> _toggleMicroRotation(bool currentValue) async {
    if (_isTogglingMicroRotation) return;
    final newValue = !currentValue;
    final messenger = ScaffoldMessenger.of(context);

    if (newValue) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Enable Micro Rotation Mode?'),
          content: const Text(
            'Micro Rotation Mode (§D) limits the engine to:\n\n'
            '• One active pending trade at a time\n'
            '• No staggered ladder\n'
            '• BUY_LIMIT / BUY_STOP only\n'
            '• Mandatory TP and expiry on every order\n\n'
            'Designed for safe live testing with a small free balance. All safety rules still apply.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Enable Micro Rotation'),
            ),
          ],
        ),
      );
      if (confirmed != true) return;
    }

    setState(() => _isTogglingMicroRotation = true);
    try {
      await ref.read(brainApiProvider).setMicroRotationEnabled(newValue);
      ref.invalidate(runtimeSettingsProvider);
      messenger.showSnackBar(
        SnackBar(
          content: Text(newValue
              ? '🔬 Micro Rotation Mode ENABLED — single trade, no ladder.'
              : '📊 Micro Rotation Mode DISABLED — full rotation restored.'),
          duration: const Duration(seconds: 4),
        ),
      );
    } catch (error) {
      messenger.showSnackBar(
        SnackBar(content: Text('Failed to toggle Micro Rotation: $error')),
      );
    } finally {
      if (mounted) setState(() => _isTogglingMicroRotation = false);
    }
  }

  Widget _buildMicroRotationCard(AsyncValue<RuntimeSettings> runtimeSettings) {
    final cs = Theme.of(context).colorScheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: runtimeSettings.when(
          data: (settings) {
            final enabled = settings.microRotationEnabled;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Micro Rotation Mode (§D): limits the engine to one active pending trade at a time with no ladder. '
                  'Designed for safe live testing with a small free balance (e.g. 2,237 AED) while keeping existing gold inventory separate.\n\n'
                  'All safety rules, PRETABLE gates, and Pending-Before-Level Law still apply.',
                  style: TextStyle(fontSize: 13),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Micro Rotation Mode',
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.copyWith(fontWeight: FontWeight.bold)),
                          const SizedBox(height: 6),
                          Text(
                            enabled
                                ? '🔬 ON — single trade, no ladder'
                                : '📊 OFF — full rotation active',
                            style: TextStyle(
                                color: enabled ? Colors.teal.shade700 : null),
                          ),
                        ],
                      ),
                    ),
                    if (_isTogglingMicroRotation)
                      const SizedBox(
                        width: 24,
                        height: 24,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    else
                      Switch(
                        value: enabled,
                        onChanged: (_) => _toggleMicroRotation(enabled),
                        activeThumbColor: Colors.teal.shade600,
                      ),
                  ],
                ),
              ],
            );
          },
          loading: () => const LinearProgressIndicator(),
          error: (e, _) => Text('Error loading settings: $e',
              style: TextStyle(color: cs.error)),
        ),
      ),
    );
  }
}