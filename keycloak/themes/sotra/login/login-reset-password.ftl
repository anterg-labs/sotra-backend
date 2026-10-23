<#-- «Recuperar contraseña» con el orden de la pantalla que tenía el frontend: instrucción, correo, «Enviar enlace» y volver. -->
<#import "template.ftl" as layout>
<@layout.registrationLayout displayInfo=false displayMessage=!messagesPerField.existsError('username'); section>
    <#if section = "header">
        ${msg("emailForgotTitle")}
    <#elseif section = "form">
        <form id="kc-reset-password-form" action="${url.loginAction}" method="post">
            <p class="sotra-instruccion">${msg("emailInstruction")}</p>
            <div class="${properties.kcFormGroupClass!}">
                <label for="username" class="${properties.kcLabelClass!}">${msg("sotraCorreo")}</label>
                <input type="text" id="username" name="username" class="${properties.kcInputClass!}" autofocus autocomplete="email"
                       value="${(auth.attemptedUsername!'')}" aria-invalid="<#if messagesPerField.existsError('username')>true</#if>" dir="ltr"/>
                <#if messagesPerField.existsError('username')>
                    <span id="input-error-username" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
                        ${kcSanitize(messagesPerField.get('username'))?no_esc}
                    </span>
                </#if>
            </div>
            <div id="kc-form-buttons">
                <input class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!}" type="submit" value="${msg("sotraEnviarEnlace")}"/>
            </div>
            <p class="sotra-volver"><a href="${url.loginUrl}">${msg("backToLogin")}</a></p>
        </form>
    </#if>
</@layout.registrationLayout>
