<#--
  Estructura de todas las pantallas de login de SOTRA (iniciar sesión, recuperar y elegir contraseña, errores).
  El <head> es el del tema "base" de Keycloak 26 (scripts de sesión y de contraseña); el <body> reproduce la
  pantalla de login que tenía el frontend: formulario con el logo de CVM a la izquierda y la foto de maquinaria
  con el lema a la derecha (solo en pantallas anchas).
-->
<#import "footer.ftl" as loginFooter>
<#import "theme-resources.ftl" as themeResourceTags>
<#macro registrationLayout bodyClass="" displayInfo=false displayMessage=true displayRequiredFields=false>
<!DOCTYPE html>
<html class="${properties.kcHtmlClass!}" lang="${lang}"<#if realm.internationalizationEnabled> dir="${(locale.rtl)?then('rtl','ltr')}"</#if>>

<head>
    <meta charset="utf-8">
    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />

    <#if properties.meta?has_content>
        <#list properties.meta?split(' ') as meta>
            <meta name="${meta?split('==')[0]}" content="${meta?split('==')[1]}"/>
        </#list>
    </#if>
    <title>${title!}</title>
    <link rel="icon" href="${url.resourcesPath}/img/favicon.ico" />
    <#if properties.styles?has_content>
        <#list properties.styles?split(' ') as style>
            <link href="${url.resourcesPath}/${style}" rel="stylesheet" />
        </#list>
    </#if>
    <#if properties.scripts?has_content>
        <#list properties.scripts?split(' ') as script>
            <script src="${url.resourcesPath}/${script}" type="text/javascript"></script>
        </#list>
    </#if>
    <script type="importmap">
        {
            "imports": {
                "rfc4648": "${url.resourcesCommonPath}/vendor/rfc4648/rfc4648.js"
            }
        }
    </script>
    <script src="${url.resourcesPath}/js/menu-button-links.js" type="module"></script>
    <#if scripts??>
        <#list scripts as script>
            <script src="${script}" type="text/javascript"></script>
        </#list>
    </#if>
    <script type="module">
        <#outputformat "JavaScript">
        import { startSessionPolling } from ${(url.resourcesPath + "/js/authChecker.js")?c};

        startSessionPolling(
            ${url.ssoLoginInOtherTabsUrl?c}
        );
        </#outputformat>
    </script>
    <script type="module">
        document.addEventListener("click", (event) => {
            const link = event.target.closest("a[data-once-link]");

            if (!link) {
                return;
            }

            if (link.getAttribute("aria-disabled") === "true") {
                event.preventDefault();
                return;
            }

            const { disabledClass } = link.dataset;

            if (disabledClass) {
                link.classList.add(...disabledClass.trim().split(/\s+/));
            }

            link.setAttribute("role", "link");
            link.setAttribute("aria-disabled", "true");
        });
    </script>
    <#if authenticationSession??>
        <script type="module">
            <#outputformat "JavaScript">
            import { checkAuthSession } from ${(url.resourcesPath + "/js/authChecker.js")?c};

            checkAuthSession(
                ${authenticationSession.authSessionIdHash?c}
            );
            </#outputformat>
        </script>
    </#if>
</head>

<body class="sotra-cuerpo ${bodyClass}" data-page-id="login-${pageId}">
<main class="sotra-login">
    <div class="sotra-login-lado-formulario">
        <section class="sotra-login-tarjeta">
            <img alt="CVM" class="sotra-login-logo" height="152" src="${url.resourcesPath}/img/logo-cvm.png" width="717" />

            <#if !(auth?has_content && auth.showUsername() && !auth.showResetCredentials())>
                <h1 id="kc-page-title" class="sotra-titulo"><#nested "header"></h1>
                <#if pageId == "login">
                    <p class="sotra-subtitulo">${msg("sotraSubtitulo")}</p>
                </#if>
            <#else>
                <#nested "show-username">
                <div id="kc-username" class="sotra-usuario-intentado">
                    <span id="kc-attempted-username">${auth.attemptedUsername}</span>
                    <a id="reset-login" href="${url.loginRestartFlowUrl}">${msg("restartLoginTooltip")}</a>
                </div>
            </#if>

            <#-- Las acciones iniciadas por la aplicación no muestran el aviso de que hay que completarlas. -->
            <#if displayMessage && message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                <div class="${properties.kcAlertClass!} sotra-aviso-${message.type}" role="<#if message.type = 'error'>alert<#else>status</#if>">
                    <span class="${properties.kcAlertTitleClass!}">${kcSanitize(message.summary)?no_esc}</span>
                </div>
            </#if>

            <div class="sotra-contenido">
                <#nested "form">

                <#if auth?has_content && auth.showTryAnotherWayLink()>
                    <form id="kc-select-try-another-way-form" action="${url.loginAction}" method="post">
                        <input type="hidden" name="tryAnotherWay" value="on"/>
                        <a href="#" id="try-another-way" class="sotra-enlace"
                           onclick="document.forms['kc-select-try-another-way-form'].requestSubmit();return false;">${msg("doTryAnotherWay")}</a>
                    </form>
                </#if>

                <#nested "socialProviders">

                <#if displayInfo>
                    <div id="kc-info" class="sotra-info">
                        <#nested "info">
                    </div>
                </#if>
            </div>

            <@loginFooter.content/>
        </section>
    </div>

    <aside class="sotra-login-lado-imagen" aria-hidden="true">
        <img alt="" class="sotra-login-foto" src="${url.resourcesPath}/img/login-maquinaria.jpg" />
        <div class="sotra-login-degradado"></div>
        <div class="sotra-login-lema">
            <span class="sotra-login-barra"></span>
            <p class="sotra-login-lema-titulo">${kcSanitize(msg("sotraLema"))?no_esc}</p>
            <p class="sotra-login-lema-texto">${msg("sotraDescripcion")}</p>
        </div>
    </aside>
</main>
</body>
</html>
</#macro>
