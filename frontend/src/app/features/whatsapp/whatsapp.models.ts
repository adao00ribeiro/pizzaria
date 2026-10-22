export interface WhatsAppMessage {
  id: string;
  sender: 'customer' | 'agent' | 'system' | 'ai';
  text: string;
  timestamp: string;
  status?: 'sent' | 'delivered' | 'read';
}

export interface AiOrderSuggestion {
  detected: boolean;
  summary: string;
  itemsDescription: string;
  suggestedType: 'Delivery' | 'Pickup' | 'Counter';
  deliveryAddress?: string;
  estimatedTotal?: number;
  confidence: number;
  extractedNotes?: string;
}

export interface QuickReply {
  id: string;
  title: string;
  text: string;
  icon?: string;
}

export interface WhatsAppContact {
  id: string;
  name: string;
  phone: string;
  avatarUrl?: string;
  profilePicUrl?: string;
  unreadCount: number;
  lastMessageTime: string;
  lastMessageText: string;
  hasActiveOrder: boolean;
  activeOrderCode?: string;
  activeOrderStatus?: string;
  address?: string;
  messages: WhatsAppMessage[];
  aiSuggestion?: AiOrderSuggestion;
  aiSuggestedReplies?: string[];
}

