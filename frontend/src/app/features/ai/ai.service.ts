import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AiFeedback,
  AiHealth,
  AiUsage,
  ChatAnswer,
  ChatRequest,
  ConversationDetail,
  ConversationSummary,
} from '../../core/models/ai.models';

@Injectable({ providedIn: 'root' })
export class AiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/ai`;

  chat(message: string, conversationId?: string | null): Observable<ChatAnswer> {
    const payload: ChatRequest = { message, conversationId };
    return this.http.post<ChatAnswer>(`${this.baseUrl}/chat`, payload);
  }

  suggestions(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/suggestions`);
  }

  health(): Observable<AiHealth> {
    return this.http.get<AiHealth>(`${this.baseUrl}/health`);
  }

  listConversations(includeArchived: boolean = false): Observable<ConversationSummary[]> {
    const params = new HttpParams().set('includeArchived', includeArchived);
    return this.http.get<ConversationSummary[]>(`${this.baseUrl}/conversations`, { params });
  }

  getConversation(id: string): Observable<ConversationDetail> {
    return this.http.get<ConversationDetail>(`${this.baseUrl}/conversations/${id}`);
  }

  deleteConversation(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/conversations/${id}`);
  }

  usage(): Observable<AiUsage> {
    return this.http.get<AiUsage>(`${this.baseUrl}/usage`);
  }

  feedback(feedback: AiFeedback): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/feedback`, feedback);
  }
}
