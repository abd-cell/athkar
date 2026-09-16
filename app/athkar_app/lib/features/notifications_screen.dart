import 'package:flutter/material.dart';

import '../core/l10n.dart';
import '../core/theme.dart';
import '../models/models.dart';
import '../services/services.dart';
import '../widgets/athkar_ui.dart';

/// The inbox.
///
/// Every reminder and broadcast is stored server-side as a row for this device
/// as well as pushed, so a reader who had notifications off — or whose phone was
/// in a tunnel — still finds the message here. The push is a courtesy; this is
/// the delivery.
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  List<NotificationItem>? _items;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final response = await Api.devices.notifications();

    if (!mounted) return;
    setState(() {
      _items = response.data ?? const [];
      _error = response.success ? null : response.errorMessage;
    });
  }

  @override
  Widget build(BuildContext context) {
    final tokens = AthkarTokens.of(context);
    final items = _items;

    return Scaffold(
      appBar: AppBar(
        title: Text(context.tr('notifications.title')),
        actions: [
          if (items != null && items.any((item) => !item.isRead))
            TextButton(
              onPressed: () async {
                await Api.devices.markAllRead();
                await _load();
              },
              child: Text(context.tr('notifications.markAllRead')),
            ),
        ],
      ),
      body: items == null
          ? const Center(child: AthkarSpinner())
          : items.isEmpty
              ? AthkarEmptyState(
                  title: _error ?? context.tr('notifications.empty'),
                  body: _error == null ? context.tr('notifications.emptyHint') : null,
                  icon: Icons.notifications_none,
                )
              : RefreshIndicator(
                  onRefresh: _load,
                  color: tokens.brand,
                  child: ListView.separated(
                    padding: const EdgeInsets.fromLTRB(
                      AthkarSpacing.page, 12, AthkarSpacing.page, 30),
                    itemCount: items.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 10),
                    itemBuilder: (context, index) {
                      final item = items[index];

                      return AthkarCard(
                        radius: AthkarSpacing.smallCardRadius,
                        onTap: item.isRead
                            ? null
                            : () async {
                                await Api.devices.markRead(item.id);
                                await _load();
                              },
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                if (!item.isRead) ...[
                                  Container(
                                    width: 7,
                                    height: 7,
                                    decoration: BoxDecoration(
                                      shape: BoxShape.circle,
                                      color: tokens.brand,
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                ],
                                Expanded(
                                  child: Text(
                                    item.title,
                                    style: AthkarType.sans(
                                      size: 13.5,
                                      color: tokens.ink,
                                      weight: FontWeight.w600,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 6),
                            Text(
                              item.body,
                              style: AthkarType.sans(
                                size: 13, color: tokens.muted, height: 1.7),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
    );
  }
}
