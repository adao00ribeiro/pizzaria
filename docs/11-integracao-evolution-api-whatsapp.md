# Documentação de Integração: Evolution API (WhatsApp)

Esta documentação descreve a arquitetura, configuração Docker, rotas REST utilizadas, fluxo de autenticação e comunicação entre o frontend da **Pizzaria Lucrativa** e a **Evolution API v2**.

---

## 1. Visão Geral da Arquitetura

A integração do WhatsApp com a Pizzaria Lucrativa utiliza a **Evolution API v2** executando em containers Docker dedicados. A solução suporta sincronização em tempo real de chats, fotos de perfil, histórico de mensagens e envio de mensagens para clientes (confirmação de pedidos, cardápio digital, chave Pix e acompanhamento de entrega).

```mermaid
flowchart TD
    subgraph Frontend ["Frontend (Angular 18)"]
        WPage["WhatsAppPage (/whatsapp)"]
        WService["WhatsAppService"]
        WDialog["WhatsAppConnectDialog (QR Code)"]
    end

    subgraph Docker ["Docker Stack (docker-compose.evolution.yml)"]
        EvoAPI["Evolution API v2 (:8080)"]
        Postgres["PostgreSQL 15 (:5433 / evolution_db)"]
        Redis["Redis (:6379)"]
    end

    subgraph WhatsAppNet ["Rede WhatsApp / Meta"]
        WDevice["WhatsApp no Celular (44) 99924-6859"]
        MetaServers["Servidores WhatsApp (Baileys)"]
    end

    WPage --> WService
    WDialog --> WService
    WService -->|"REST HTTP (Header: apikey)"| EvoAPI
    EvoAPI -->|"Persistência (chats, mensagens)"| Postgres
    EvoAPI -->|"Sessão & Caching Baileys"| Redis
    EvoAPI <-->|"WebSocket / Baileys"| MetaServers
    MetaServers <-->|"Conexão Pareada"| WDevice
```

---

## 2. Infraestrutura Docker

O ambiente da Evolution API está isolado no arquivo [`docker-compose.evolution.yml`](file:///media/jarvis/Novo%20volume/AspNetProjects/ESTUDOCASO/docker-compose.evolution.yml):

### 2.1. Serviços

| Serviço | Imagem | Porta Host | Finalidade |
| :--- | :--- | :--- | :--- |
| **`evolution_api`** | `evoapicloud/evolution-api:latest` | `8080:8080` | Servidor REST e gateway Baileys |
| **`evolution_postgres`** | `postgres:15-alpine` | `5433:5432` | Banco de dados para histórico de mensagens e chats |
| **`evolution_redis`** | `redis:alpine` | `6379:6379` | Cache de sessões Baileys (obrigatório na v2) |

### 2.2. Variáveis de Ambiente Principais

- `SERVER_URL`: `http://localhost:8080`
- `AUTHENTICATION_API_KEY`: `pizzaria-lucrativa-secret-key-2026`
- `DATABASE_PROVIDER`: `postgresql`
- `DATABASE_CONNECTION_URI`: `postgresql://evolution:evolution_password@postgres:5432/evolution_db?schema=public`
- `DATABASE_SAVE_DATA_INSTANCE`: `true`
- `DATABASE_SAVE_DATA_NEW_MESSAGE`: `true`
- `DATABASE_SAVE_MESSAGE_UPDATE`: `true`
- `DATABASE_SAVE_DATA_CONTACTS`: `true`
- `DATABASE_SAVE_DATA_CHATS`: `true`
- `DATABASE_SAVE_DATA_HISTORIC`: `true`
- `CACHE_REDIS_ENABLED`: `true`
- `CACHE_REDIS_URI`: `redis://redis:6379/1`

### 2.3. Comandos Úteis do Docker

```bash
# Subir toda a infraestrutura em background
docker compose -f docker-compose.evolution.yml up -d

# Visualizar logs da Evolution API em tempo real
docker logs -f evolution_api

# Verificar status dos containers
docker ps --filter "name=evolution"

# Reiniciar a API
docker restart evolution_api

# Parar os containers (mantendo os dados persistidos nos volumes)
docker compose -f docker-compose.evolution.yml down
```

---

## 3. Autenticação e Cabeçalhos REST

Todas as requisições enviadas pelo frontend ou backend para a Evolution API necessitam do cabeçalho de autenticação:

```http
apikey: pizzaria-lucrativa-secret-key-2026
Content-Type: application/json
```

---

## 4. Rotas e Endpoints Utilizados no Sistema

A instância padrão configurada no sistema chama-se **`pizzaria`**.

### 4.1. Conexão e Gerenciamento da Instância

#### 1. Criar Instância (Setup Inicial)
- **Método**: `POST`
- **URL**: `http://localhost:8080/instance/create`
- **Body**:
  ```json
  {
    "instanceName": "pizzaria",
    "token": "pizzaria-token-2026",
    "qrcode": true,
    "integration": "WHATSAPP-BAILEYS"
  }
  ```

#### 2. Obter QR Code Real de Pareamento
- **Método**: `GET`
- **URL**: `http://localhost:8080/instance/connect/pizzaria`
- **Uso**: Chamado por [`WhatsAppConnectDialog`](file:///media/jarvis/Novo%20volume/AspNetProjects/ESTUDOCASO/frontend/src/app/features/whatsapp/whatsapp-connect.dialog.ts).
- **Resposta Sucesso (quando desconectado)**:
  ```json
  {
    "pairingCode": null,
    "code": "2@...",
    "base64": "data:image/png;base64,iVBORw0KGgo..."
  }
  ```
- **Resposta (quando já conectado)**:
  ```json
  {
    "instance": {
      "instanceName": "pizzaria",
      "state": "open"
    }
  }
  ```

#### 3. Checar Estado da Conexão
- **Método**: `GET`
- **URL**: `http://localhost:8080/instance/connectionState/pizzaria`
- **Uso**: Chamado na inicialização e polling leve para monitorar status em tempo real.
- **Resposta**:
  ```json
  {
    "instance": {
      "instanceName": "pizzaria",
      "state": "open" // "open" (conectado), "close" (desconectado), "connecting"
    }
  }
  ```

#### 4. Listar Instâncias e Dados do Aparelho
- **Método**: `GET`
- **URL**: `http://localhost:8080/instance/fetchInstances`
- **Uso**: Obtém metadados da conexão, como `connectionStatus: "open"` e o `ownerJid` (número do celular pareado).
- **Exemplo de Resposta**:
  ```json
  [
    {
      "name": "pizzaria",
      "connectionStatus": "open",
      "ownerJid": "554499246859@s.whatsapp.net"
    }
  ]
  ```

#### 5. Desconectar / Desemparelhar Aparelho
- **Método**: `DELETE`
- **URL**: `http://localhost:8080/instance/logout/pizzaria`
- **Uso**: Desconecta a sessão atual no WhatsApp para permitir a leitura de um novo QR Code.

---

### 4.2. Sincronização de Conversas e Mensagens

#### 1. Buscar Todas as Conversas (Chats)
- **Método**: `POST`
- **URL**: `http://localhost:8080/chat/findChats/pizzaria`
- **Body**: `{}`
- **Uso**: Chamado por `WhatsAppService.loadRealChats()`.
- **Campos Importantes Retornados**:
  - `remoteJid`: Identificador único (`554499246859@s.whatsapp.net` ou JID de grupo).
  - `pushName`: Nome de exibição do remetente.
  - `unreadCount`: Quantidade de mensagens não lidas.
  - `lastMessage`: Objeto contendo o timestamp e texto da mensagem recente.

#### 2. Buscar Contatos da Agenda
- **Método**: `POST`
- **URL**: `http://localhost:8080/chat/findContacts/pizzaria`
- **Body**: `{}`
- **Uso**: Cruzado com a lista de chats para obter a foto de perfil (`profilePicUrl`) e o nome salvo na agenda.

#### 3. Buscar Mensagens de um Chat Específico
- **Método**: `POST`
- **URL**: `http://localhost:8080/chat/findMessages/pizzaria`
- **Body**:
  ```json
  {
    "where": {
      "key": {
        "remoteJid": "5544998796670@s.whatsapp.net"
      }
    }
  }
  ```
- **Uso**: Chamado ao clicar em um contato na coluna esquerda.
- **Campos Mapeados**:
  - `key.fromMe`: `true` (enviado pela pizzaria/atendente) ou `false` (enviado pelo cliente).
  - `message.conversation` ou `message.extendedTextMessage.text`: conteúdo textual.
  - `messageTimestamp`: data/hora de envio.
  - `status`: confirmação de entrega ou leitura.

---

### 4.3. Envio de Mensagens

#### Enviar Mensagem de Texto
- **Método**: `POST`
- **URL**: `http://localhost:8080/message/sendText/pizzaria`
- **Body**:
  ```json
  {
    "number": "5544998796670",
    "text": "🍕 Olá! Seu pedido #1042 foi confirmado e já está no forno!"
  }
  ```
- **Resposta Sucesso**:
  ```json
  {
    "key": {
      "remoteJid": "5544998796670@s.whatsapp.net",
      "fromMe": true,
      "id": "3EB0..."
    },
    "message": {
      "conversation": "..."
    },
    "messageTimestamp": "1729458900",
    "status": "PENDING"
  }
  ```

---

## 5. Implementação no Frontend ([`WhatsAppService`](file:///media/jarvis/Novo%20volume/AspNetProjects/ESTUDOCASO/frontend/src/app/features/whatsapp/whatsapp.service.ts))

O frontend interage com a API via Angular Signals e Observables:

1. **Estado Conectado / Desconectado**:
   - `isConnected()`: Controla se a grade de chats é exibida ou se exibe o estado vazio com o botão para escanear QR Code.
2. **Polling Automático**:
   - Polling a cada 5 segundos que executa `loadRealChats()` e `loadMessagesForChat()` para o contato ativo, garantindo que novas mensagens recebidas no celular surjam imediatamente na tela sem refresh manual.
3. **Busca Filtrada Inteligente**:
   - `filteredContacts()`: Filtra tanto por nome, texto da mensagem e dígitos do telefone (`qDigits.length >= 2`).
4. **Criação de Pedidos Integrada**:
   - Ao clicar em "Criar Pedido" no chat, o modal `NewOrderDialog` abre automaticamente preenchido com o nome e telefone do cliente. Ao concluir o pedido, uma mensagem de confirmação com o código gerado é despachada automaticamente no WhatsApp do cliente via `sendMessage()`.

---

## 6. Próximos Passos: Webhooks e Automação com I.A

Para evolução futura (robô de pedidos via IA):
- **Configuração de Webhook na Evolution API**:
  - **URL**: `POST http://localhost:8080/webhook/set/pizzaria`
  - **Body**:
    ```json
    {
      "enabled": true,
      "url": "http://host.docker.internal:5000/api/whatsapp/webhook",
      "events": [
        "MESSAGES_UPSERT",
        "CONNECTION_UPDATE"
      ]
    }
    ```
- O backend .NET poderá receber as mensagens recebidas em tempo real, processar via LLM (ex: modelo Gemini / Antigravity), extrair pizzas, bordas e endereço, e retornar a sugestão de pedido pronta para o atendente aprovar.
