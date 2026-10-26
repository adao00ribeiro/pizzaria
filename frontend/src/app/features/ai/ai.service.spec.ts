import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { ChatAnswer, ConversationDetail, ConversationSummary } from '../../core/models/ai.models';
import { AiService } from './ai.service';

describe('AiService', () => {
  let service: AiService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/ai`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AiService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(AiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('chat() should post message and optional conversationId', () => {
    const mockAnswer: Partial<ChatAnswer> = {
      conversationId: 'c1',
      messageId: 'm1',
      text: 'Total de lucro foi R$ 1.000',
      blocks: [],
    };

    service.chat('Quanto lucrei?', 'c1').subscribe((res) => {
      expect(res.conversationId).toBe('c1');
      expect(res.text).toBe('Total de lucro foi R$ 1.000');
    });

    const req = httpMock.expectOne(`${baseUrl}/chat`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ message: 'Quanto lucrei?', conversationId: 'c1' });
    req.flush(mockAnswer);
  });

  it('suggestions() should get list of strings', () => {
    const mockSuggestions = ['Quanto lucrei no mês?', 'Quais os produtos mais vendidos?'];

    service.suggestions().subscribe((list) => {
      expect(list.length).toBe(2);
      expect(list[0]).toBe('Quanto lucrei no mês?');
    });

    const req = httpMock.expectOne(`${baseUrl}/suggestions`);
    expect(req.request.method).toBe('GET');
    req.flush(mockSuggestions);
  });

  it('listConversations() should query conversations endpoint', () => {
    const mockSummaries: ConversationSummary[] = [
      {
        id: '123',
        title: 'Lucro de Setembro',
        createdAt: '2026-10-01T10:00:00Z',
        updatedAt: '2026-10-01T10:05:00Z',
        messageCount: 2,
        isArchived: false,
      },
    ];

    service.listConversations(false).subscribe((list) => {
      expect(list.length).toBe(1);
      expect(list[0].title).toBe('Lucro de Setembro');
    });

    const req = httpMock.expectOne((r) => r.url === `${baseUrl}/conversations`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('includeArchived')).toBe('false');
    req.flush(mockSummaries);
  });

  it('getConversation() should return detail by id', () => {
    const mockDetail: ConversationDetail = {
      id: 'abc',
      title: 'Conversa Teste',
      createdAt: '2026-10-01T10:00:00Z',
      updatedAt: '2026-10-01T10:00:00Z',
      isArchived: false,
      messages: [],
    };

    service.getConversation('abc').subscribe((detail) => {
      expect(detail.id).toBe('abc');
    });

    const req = httpMock.expectOne(`${baseUrl}/conversations/abc`);
    expect(req.request.method).toBe('GET');
    req.flush(mockDetail);
  });

  it('deleteConversation() should delete by id', () => {
    service.deleteConversation('abc').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/conversations/abc`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
