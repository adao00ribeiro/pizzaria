# WhatsApp Omnichannel Feature

## Overview
WhatsApp conversational commerce console supporting instance QR code linking, real-time customer chats, AI-assisted order extraction, and quick response templates.

## Interface
- `whatsapp.page.ts:WhatsAppPage`: Chat workspace managing active customer threads, incoming messages, and order conversion actions.
- `whatsapp.service.ts:WhatsAppService`: HTTP and polling service interacting with `/whatsapp/instance`, `/whatsapp/chats`, `/whatsapp/messages`, and `/whatsapp/ai-order-suggestion`.
- `whatsapp-connect.dialog.ts:WhatsAppConnectDialog`: Modal displaying Evolution/Baileys QR code for scanning and instance status monitoring.
- `whatsapp.models.ts`: Data models for messages, AI order drafts, quick replies, and contacts.

## Invariants
- `[whatsapp.service.ts:WhatsAppService]`: Polls QR code pairing status at regular intervals until connected or dialog dismissed.

## Side Effects
- `[whatsapp.service.ts:WhatsAppService]`: Dispatches HTTP calls to `/whatsapp/*`.

## Verification
- Run: `npm test --prefix frontend`
