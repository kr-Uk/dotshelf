#import <AuthenticationServices/AuthenticationServices.h>
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern void UnitySendMessage(const char *obj, const char *method, const char *msg);
extern UIViewController *UnityGetGLViewController(void);

static void DotShelfSendUnityMessage(NSString *objectName, const char *methodName, NSString *message)
{
    if (objectName.length == 0 || methodName == NULL)
    {
        return;
    }

    const char *objectNameChars = [objectName UTF8String];
    const char *messageChars = message.length > 0 ? [message UTF8String] : "";
    UnitySendMessage(objectNameChars, methodName, messageChars);
}

@interface DotShelfAppleSignInDelegate : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property(nonatomic, copy) NSString *callbackObjectName;
@end

static DotShelfAppleSignInDelegate *s_currentAppleSignInDelegate;

@implementation DotShelfAppleSignInDelegate

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller API_AVAILABLE(ios(13.0))
{
    UIViewController *unityViewController = UnityGetGLViewController();
    if (unityViewController.view.window != nil)
    {
        return unityViewController.view.window;
    }

    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
    {
        if (scene.activationState != UISceneActivationStateForegroundActive ||
            ![scene isKindOfClass:UIWindowScene.class])
        {
            continue;
        }

        UIWindowScene *windowScene = (UIWindowScene *)scene;
        for (UIWindow *window in windowScene.windows)
        {
            if (window.isKeyWindow)
            {
                return window;
            }
        }
    }

    return UIApplication.sharedApplication.keyWindow;
}

- (void)authorizationController:(ASAuthorizationController *)controller
    didCompleteWithAuthorization:(ASAuthorization *)authorization API_AVAILABLE(ios(13.0))
{
    if (![authorization.credential isKindOfClass:ASAuthorizationAppleIDCredential.class])
    {
        DotShelfSendUnityMessage(self.callbackObjectName, "OnAppleSignInFailed", @"INVALID_IDENTITY_TOKEN");
        s_currentAppleSignInDelegate = nil;
        return;
    }

    ASAuthorizationAppleIDCredential *credential = (ASAuthorizationAppleIDCredential *)authorization.credential;
    if (credential.identityToken.length == 0)
    {
        DotShelfSendUnityMessage(self.callbackObjectName, "OnAppleSignInFailed", @"MISSING_IDENTITY_TOKEN");
        s_currentAppleSignInDelegate = nil;
        return;
    }

    NSString *identityToken = [[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding];
    if (identityToken.length == 0)
    {
        DotShelfSendUnityMessage(self.callbackObjectName, "OnAppleSignInFailed", @"INVALID_IDENTITY_TOKEN");
        s_currentAppleSignInDelegate = nil;
        return;
    }

    DotShelfSendUnityMessage(self.callbackObjectName, "OnAppleSignInSucceeded", identityToken);
    s_currentAppleSignInDelegate = nil;
}

- (void)authorizationController:(ASAuthorizationController *)controller
           didCompleteWithError:(NSError *)error API_AVAILABLE(ios(13.0))
{
    NSString *errorCode = @"APPLE_SIGN_IN_FAILED";
    if ([error.domain isEqualToString:ASAuthorizationErrorDomain] &&
        error.code == ASAuthorizationErrorCanceled)
    {
        errorCode = @"CANCELLED";
    }

    DotShelfSendUnityMessage(self.callbackObjectName, "OnAppleSignInFailed", errorCode);
    s_currentAppleSignInDelegate = nil;
}

@end

extern "C" void DotshelfAppleSignIn_Start(const char *nonce, const char *callbackObjectName)
{
    NSString *callbackObject = callbackObjectName != NULL
        ? [NSString stringWithUTF8String:callbackObjectName]
        : @"";
    NSString *nonceString = nonce != NULL
        ? [NSString stringWithUTF8String:nonce]
        : @"";

    if (nonceString.length == 0)
    {
        DotShelfSendUnityMessage(callbackObject, "OnAppleSignInFailed", @"MISSING_NONCE");
        return;
    }

    if (s_currentAppleSignInDelegate != nil)
    {
        DotShelfSendUnityMessage(callbackObject, "OnAppleSignInFailed", @"ALREADY_IN_PROGRESS");
        return;
    }

    if (@available(iOS 13.0, *))
    {
        ASAuthorizationAppleIDProvider *provider = [[ASAuthorizationAppleIDProvider alloc] init];
        ASAuthorizationAppleIDRequest *request = [provider createRequest];
        request.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];
        request.nonce = nonceString;

        DotShelfAppleSignInDelegate *delegate = [[DotShelfAppleSignInDelegate alloc] init];
        delegate.callbackObjectName = callbackObject;
        s_currentAppleSignInDelegate = delegate;

        ASAuthorizationController *controller =
            [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        controller.delegate = delegate;
        controller.presentationContextProvider = delegate;
        [controller performRequests];
    }
    else
    {
        DotShelfSendUnityMessage(callbackObject, "OnAppleSignInFailed", @"UNSUPPORTED_IOS_VERSION");
    }
}
