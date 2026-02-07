# Implementation Plan: Nanobot .NET Port

## Overview

This implementation plan breaks down the .NET port of Nanobot into incremental, testable steps. The approach follows a bottom-up strategy: establish core infrastructure (configuration, DI, hosting), build the message bus and storage layers, implement integrations (LLM providers, chat platforms, tools), add agent logic, and finally wire everything together with service hosting.

Each task builds on previous work, with property-based tests placed close to implementation to catch errors early. The plan maintains the <6,000 line constraint by focusing on minimal, essential implementations.

## Tasks

- [ ] 1. Set up project structure and core infrastructure
  - Create solution with projects: Nanobot.Core, Nanobot.Host, Nanobot.Tests.Unit, Nanobot.Tests.Properties
  - Add NuGet packages: Microsoft.Extensions.Hosting, Microsoft.Extensions.Hosting.WindowsServices, Microsoft.Extensions.Hosting.Systemd, System.Text.Json, FsCheck
  - Configure nullable reference types and C# 12 features
  - Set up logging infrastructure with ILogger
  - _Requirements: 13.2, 13.4, 13.7_

- [ ] 2. Implement configuration system
  - [ ] 2.1 Create configuration models (NanobotConfiguration, LlmConfiguration, ChatPlatformConfiguration, ToolConfiguration)
    - Define strongly-typed configuration classes with data annotations
    - Implement IValidateOptions for configuration validation
    - _Requirements: 7.5, 7.6, 7.7_
  
  - [ ] 2.2 Implement Configuration_Manager with JSON and environment variable support
    - Load from appsettings.json and ~/.nanobot/config.json
    - Support environment variable overrides (${VAR_NAME} syntax)
    - Create default config file if missing
    - _Requirements: 7.1, 7.2, 7.3_
  
  - [ ]* 2.3 Write property test for configuration validation
    - **Property 12: Configuration Validation**
    - **Validates: Requirements 7.4**
  
  - [ ]* 2.4 Write unit tests for configuration loading
    - Test loading from file
    - Test environment variable overrides
    - Test default config creation
    - _Requirements: 7.1, 7.2, 7.3_

- [ ] 3. Implement message bus with pub/sub pattern
  - [ ] 3.1 Define message interfaces and types (IMessage, UserMessage, AgentResponse, ToolCall, ToolResult)
    - Create message type hierarchy with records
    - Add timestamp and ID to all messages
    - _Requirements: 3.4, 3.5_
  
  - [ ] 3.2 Implement IMessageBus using System.Threading.Channels
    - Create channel-based pub/sub with bounded capacity
    - Implement PublishAsync and SubscribeAsync methods
    - Support multiple subscribers per message type
    - _Requirements: 3.4, 3.5_
  
  - [ ]* 3.3 Write property test for message routing to handlers
    - **Property 2: Message Bus Routing to Handlers**
    - **Validates: Requirements 3.4**
  
  - [ ] 3.4 Write property test for response routing to origin platform
    - **Property 3: Response Routing to Origin Platform**
    - **Validates: Requirements 3.5**
  
  - [ ]* 3.5 Write unit tests for message bus
    - Test publish/subscribe flow
    - Test multiple subscribers
    - Test backpressure handling
    - _Requirements: 3.4, 3.5_

- [ ] 4. Implement memory store with JSON persistence
  - [ ] 4.1 Create ConversationMessage model and storage structure
    - Define conversation storage format
    - Implement directory structure: ~/.nanobot/memory/{platform}/{user_id}/
    - _Requirements: 6.1, 6.2, 6.3_
  
  - [ ] 4.2 Implement IMemoryStore with file-based JSON storage
    - Implement SaveMessageAsync for persistence
    - Implement GetHistoryAsync for querying
    - Use System.Text.Json for serialization
    - _Requirements: 6.1, 6.2, 6.4, 6.5_
  
  - [ ]* 4.3 Write property test for message persistence
    - **Property 10: Message Persistence**
    - **Validates: Requirements 6.1, 6.2**
  
  - [ ]* 4.4 Write property test for conversation history round-trip
    - **Property 11: Conversation History Round-Trip**
    - **Validates: Requirements 6.3, 6.5**
  
  - [ ]* 4.5 Write unit tests for memory store
    - Test saving and loading messages
    - Test querying by user, platform, time range
    - Test JSON serialization format
    - _Requirements: 6.4_

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implement tool registry and built-in tools
  - [ ] 6.1 Define IToolDefinition interface and ToolResult model
    - Create tool abstraction with ExecuteAsync and GetSchema
    - Define parameter validation using JSON schema
    - _Requirements: 5.6, 5.7_
  
  - [ ] 6.2 Implement ToolRegistry with parameter validation
    - Create dictionary-based tool registry
    - Implement parameter validation against schemas
    - Add workspace path validation
    - _Requirements: 5.6, 11.1_
  
  - [ ] 6.3 Implement FileReadTool with workspace sandboxing
    - Validate paths are within workspace
    - Read file contents and return as string
    - _Requirements: 5.1, 5.9_
  
  - [ ] 6.4 Implement FileWriteTool with workspace sandboxing
    - Validate paths are within workspace
    - Write content to file with proper error handling
    - _Requirements: 5.2, 5.9_
  
  - [ ] 6.5 Implement ShellTool with timeout enforcement
    - Execute shell commands with Process API
    - Enforce 30-second timeout
    - Capture stdout and stderr
    - _Requirements: 5.3, 11.2_
  
  - [ ] 6.6 Implement WebFetchTool using HttpClient
    - Fetch web pages via HTTP/HTTPS
    - Handle timeouts and errors
    - _Requirements: 5.4_
  
  - [ ] 6.7 Implement SendMessageTool for chat platform integration
    - Send messages through chat platforms
    - Queue messages to message bus
    - _Requirements: 5.5_
  
  - [ ]* 6.8 Write property test for tool execution on request
    - **Property 7: Tool Execution on Request**
    - **Validates: Requirements 5.6**
  
  - [ ]* 6.9 Write property test for tool result return
    - **Property 8: Tool Result Return**
    - **Validates: Requirements 5.7**
  
  - [ ]* 6.10 Write property test for workspace path validation
    - **Property 9: Workspace Path Validation**
    - **Validates: Requirements 5.9, 11.3, 11.4**
  
  - [ ]* 6.11 Write property test for tool parameter validation
    - **Property 20: Tool Parameter Validation**
    - **Validates: Requirements 11.1**
  
  - [ ]* 6.12 Write property test for shell command privilege restriction
    - **Property 21: Shell Command Privilege Restriction**
    - **Validates: Requirements 11.6**
  
  - [ ]* 6.13 Write unit tests for built-in tools
    - Test each tool with specific examples
    - Test shell timeout edge case
    - Test file operation edge cases
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [ ] 7. Implement LLM provider abstraction and clients
  - [ ] 7.1 Define ILlmProvider interface and request/response models
    - Create LlmRequest and LlmResponse models
    - Define CompleteAsync and StreamCompleteAsync methods
    - Support function calling in request format
    - _Requirements: 4.7, 4.8, 4.9_
  
  - [ ] 7.2 Implement OpenRouterClient with HttpClient
    - Implement API client for OpenRouter
    - Support function calling
    - Support streaming responses
    - _Requirements: 4.1, 4.9_
  
  - [ ] 7.3 Implement AnthropicClient with HttpClient
    - Implement API client for Anthropic Claude
    - Support tool use (Anthropic's function calling)
    - Support streaming responses
    - _Requirements: 4.2, 4.9_
  
  - [ ] 7.4 Implement OpenAIClient with HttpClient
    - Implement API client for OpenAI
    - Support function calling
    - Support streaming responses
    - _Requirements: 4.3, 4.9_
  
  - [ ] 7.5 Implement DeepSeekClient with HttpClient
    - Implement API client for DeepSeek
    - Support function calling
    - _Requirements: 4.4_
  
  - [ ] 7.6 Implement GroqClient with HttpClient
    - Implement API client for Groq
    - Support function calling
    - _Requirements: 4.5_
  
  - [ ] 7.7 Implement GeminiClient with HttpClient
    - Implement API client for Google Gemini
    - Support function calling
    - _Requirements: 4.6_
  
  - [ ]* 7.8 Write property test for provider configuration consistency
    - **Property 5: Provider Configuration Consistency**
    - **Validates: Requirements 4.7**
  
  - [ ]* 7.9 Write property test for LLM error handling
    - **Property 6: LLM Error Handling**
    - **Validates: Requirements 4.8**
  
  - [ ]* 7.10 Write unit tests for LLM providers
    - Test each provider with mock HTTP responses
    - Test streaming functionality
    - Test error handling
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.9_

- [ ] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Implement chat platform adapters
  - [ ] 9.1 Define IChatPlatform interface
    - Create interface with ConnectAsync, SendMessageAsync, DisconnectAsync
    - Define platform-specific configuration models
    - _Requirements: 3.1, 3.2, 3.3_
  
  - [ ] 9.2 Implement TelegramAdapter using Telegram Bot API
    - Use HttpClient for Telegram Bot API calls
    - Implement long polling for receiving messages
    - Publish received messages to message bus
    - _Requirements: 3.1, 3.4_
  
  - [ ] 9.3 Implement WhatsAppAdapter using WhatsApp Business API
    - Use HttpClient for WhatsApp API calls
    - Implement webhook receiver for messages
    - Publish received messages to message bus
    - _Requirements: 3.2, 3.4_
  
  - [ ] 9.4 Implement FeishuAdapter using Feishu Open API
    - Use HttpClient for Feishu API calls
    - Implement event subscription for messages
    - Publish received messages to message bus
    - _Requirements: 3.3, 3.4_
  
  - [ ]* 9.5 Write property test for connection failure recovery
    - **Property 4: Connection Failure Recovery**
    - **Validates: Requirements 3.6**
  
  - [ ]* 9.6 Write unit tests for chat platform adapters
    - Test each adapter with mock API responses
    - Test message sending and receiving
    - Test connection error handling
    - _Requirements: 3.1, 3.2, 3.3_

- [ ] 10. Implement agent service with LLM orchestration
  - [ ] 10.1 Create AgentService as IHostedService
    - Subscribe to UserMessage from message bus
    - Implement main message processing loop
    - _Requirements: 3.4, 5.6, 5.7_
  
  - [ ] 10.2 Implement conversation context management
    - Load conversation history from memory store
    - Build LLM request with history and system prompt
    - Format tool definitions for LLM
    - _Requirements: 6.3, 5.6_
  
  - [ ] 10.3 Implement tool calling loop
    - Parse tool calls from LLM response
    - Execute tools via tool registry
    - Continue conversation with tool results
    - Handle multi-turn tool calling
    - _Requirements: 5.6, 5.7_
  
  - [ ] 10.4 Implement response publishing
    - Publish AgentResponse to message bus
    - Save messages to memory store
    - _Requirements: 3.5, 6.1, 6.2_
  
  - [ ]* 10.5 Write unit tests for agent service
    - Test message processing flow
    - Test tool calling loop
    - Test conversation context building
    - _Requirements: 5.6, 5.7_

- [ ] 11. Implement logging infrastructure
  - [ ] 11.1 Configure structured logging with ILogger
    - Set up log levels from configuration
    - Add log scopes for correlation
    - _Requirements: 12.7_
  
  - [ ] 11.2 Add logging to all error paths
    - Log errors with stack traces
    - Log connection events
    - Log LLM API calls
    - Log tool executions
    - _Requirements: 12.1, 12.2, 12.3, 12.4_
  
  - [ ] 11.3 Implement platform-specific log sinks
    - Windows Event Log sink for Windows Service
    - Console sink for systemd (stdout)
    - _Requirements: 12.5, 12.6_
  
  - [ ]* 11.4 Write property test for error logging with stack traces
    - **Property 22: Error Logging with Stack Traces**
    - **Validates: Requirements 12.1**
  
  - [ ]* 11.5 Write property test for connection event logging
    - **Property 23: Connection Event Logging**
    - **Validates: Requirements 12.2**
  
  - [ ]* 11.6 Write property test for LLM API call logging
    - **Property 24: LLM API Call Logging**
    - **Validates: Requirements 12.3**
  
  - [ ]* 11.7 Write property test for tool execution logging
    - **Property 25: Tool Execution Logging**
    - **Validates: Requirements 12.4**
  
  - [ ]* 11.8 Write unit tests for logging
    - Test log level filtering
    - Test platform-specific sinks
    - _Requirements: 12.5, 12.6, 12.7_

- [ ] 12. Implement scheduler service with cron support
  - [ ] 12.1 Add Cronos library for cron expression parsing
    - Install Cronos NuGet package
    - Create ScheduledTask model with cron expression
    - _Requirements: 9.1_
  
  - [ ] 12.2 Create SchedulerService as IHostedService
    - Load scheduled tasks from configuration
    - Use PeriodicTimer for checking schedules
    - _Requirements: 9.1, 9.2_
  
  - [ ] 12.3 Implement task execution via message bus
    - Publish messages for tool executions
    - Publish messages for scheduled messages
    - _Requirements: 9.2, 9.3, 9.4_
  
  - [ ]* 12.4 Write property test for scheduled task execution
    - **Property 15: Scheduled Task Execution**
    - **Validates: Requirements 9.2**
  
  - [ ]* 12.5 Write property test for scheduled task failure isolation
    - **Property 16: Scheduled Task Failure Isolation**
    - **Validates: Requirements 9.5**
  
  - [ ]* 12.6 Write unit tests for scheduler
    - Test cron expression parsing
    - Test task triggering
    - Test error handling
    - _Requirements: 9.1, 9.3, 9.4_

- [ ] 13. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 14. Implement subagent system for background tasks
  - [ ] 14.1 Create Subagent model and state persistence
    - Define subagent state structure
    - Implement state serialization to JSON
    - _Requirements: 10.4_
  
  - [ ] 14.2 Implement SubagentManager service
    - Create subagents for long-running tasks
    - Track running subagents
    - Persist state on shutdown
    - Resume subagents on startup
    - _Requirements: 10.1, 10.4_
  
  - [ ] 14.3 Implement subagent completion notifications
    - Publish notification messages to message bus
    - Route notifications to originating platform
    - _Requirements: 10.2_
  
  - [ ] 14.4 Implement subagent cancellation
    - Support canceling running subagents
    - Clean up resources on cancellation
    - _Requirements: 10.5_
  
  - [ ]* 14.5 Write property test for subagent creation
    - **Property 17: Subagent Creation**
    - **Validates: Requirements 10.1**
  
  - [ ]* 14.6 Write property test for subagent completion notification
    - **Property 18: Subagent Completion Notification**
    - **Validates: Requirements 10.2**
  
  - [ ]* 14.7 Write property test for subagent state persistence
    - **Property 19: Subagent State Persistence**
    - **Validates: Requirements 10.4**
  
  - [ ]* 14.8 Write unit tests for subagent system
    - Test subagent lifecycle
    - Test concurrent subagents
    - Test cancellation
    - _Requirements: 10.3, 10.5_

- [ ] 15. Implement extensible skills system
  - [ ] 15.1 Define ISkill interface for plugin architecture
    - Create interface for skill registration
    - Define methods for tool and handler registration
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [ ] 15.2 Implement SkillLoader for loading .NET assemblies
    - Scan skills directory for DLL files
    - Load assemblies and discover ISkill implementations
    - Handle load failures gracefully
    - _Requirements: 8.1, 8.4, 8.5_
  
  - [ ] 15.3 Integrate skills with tool registry and message bus
    - Register skill tools with tool registry
    - Register skill handlers with message bus
    - _Requirements: 8.2, 8.3_
  
  - [ ]* 15.4 Write property test for skill tool registration
    - **Property 13: Skill Tool Registration**
    - **Validates: Requirements 8.2, 8.3**
  
  - [ ]* 15.5 Write property test for skill load failure isolation
    - **Property 14: Skill Load Failure Isolation**
    - **Validates: Requirements 8.5**
  
  - [ ]* 15.6 Write unit tests for skill system
    - Test loading valid skills
    - Test handling invalid skills
    - Test skill registration
    - _Requirements: 8.1, 8.4_

- [ ] 16. Implement service host with Windows Service and systemd support
  - [ ] 16.1 Create Program.cs with Generic Host setup
    - Configure HostBuilder with dependency injection
    - Register all services (AgentService, ChatPlatformService, SchedulerService, etc.)
    - Configure logging and configuration
    - _Requirements: 1.1, 1.2, 13.2_
  
  - [ ] 16.2 Add Windows Service support
    - Call UseWindowsService() on HostBuilder
    - Configure service name and description
    - Test service installation with sc.exe
    - _Requirements: 1.1_
  
  - [ ] 16.3 Add systemd daemon support
    - Call UseSystemd() on HostBuilder
    - Create systemd unit file template
    - Configure logging to stdout for journald
    - _Requirements: 1.2, 12.6_
  
  - [ ] 16.4 Implement graceful shutdown handling
    - Register shutdown handlers for all services
    - Persist state on shutdown
    - Close connections gracefully
    - _Requirements: 1.4_
  
  - [ ]* 16.5 Write property test for graceful shutdown persistence
    - **Property 1: Graceful Shutdown Persistence**
    - **Validates: Requirements 1.4**
  
  - [ ]* 16.6 Write unit tests for service host
    - Test service startup and shutdown
    - Test dependency injection configuration
    - _Requirements: 1.1, 1.2_

- [ ] 17. Create deployment artifacts and documentation
  - [ ] 17.1 Configure single-file publishing
    - Set up .csproj for single-file deployment
    - Configure trimming for minimal size
    - Test on Windows and Linux
    - _Requirements: 2.5_
  
  - [ ] 17.2 Create Windows Service installation script
    - PowerShell script for service installation
    - Include service configuration
    - _Requirements: 1.1_
  
  - [ ] 17.3 Create systemd unit file
    - Create nanobot.service template
    - Include restart policies
    - Document installation steps
    - _Requirements: 1.2_
  
  - [ ] 17.4 Create default configuration file
    - Generate default config.json with placeholders
    - Document all configuration options
    - _Requirements: 7.1, 7.2_
  
  - [ ] 17.5 Write README with setup instructions
    - Document installation on Windows and Linux
    - Document configuration options
    - Document skill development
    - _Requirements: 1.1, 1.2, 8.1_

- [ ] 18. Final checkpoint - Ensure all tests pass and verify line count
  - Run all unit tests and property tests
  - Verify code is under 6,000 lines (excluding comments and blank lines)
  - Test service installation on Windows and Linux
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Property tests validate universal correctness properties with 100+ iterations
- Unit tests validate specific examples and edge cases
- The implementation maintains the <6,000 line constraint through minimal, focused implementations
- All I/O operations use async/await per .NET best practices
- Dependency injection is used throughout for testability and maintainability
