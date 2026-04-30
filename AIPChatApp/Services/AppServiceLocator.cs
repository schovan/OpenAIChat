namespace AIPChatApp.Services
{
    public static class AppServiceLocator
    {
        public static IDialogService DialogService { get; private set; }
        public static IApiKeyService ApiKeyService { get; private set; }
        public static INimChatService ChatService { get; private set; }

        public static void Initialize()
        {
            DialogService = new DialogService();
            ApiKeyService = new ApiKeyService(DialogService);
            ChatService = new NimChatService(ApiKeyService);
        }
    }
}
