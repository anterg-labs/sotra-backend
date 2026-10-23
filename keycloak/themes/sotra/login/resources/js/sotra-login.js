// Mejora el formulario de ingreso sin cambiar cómo se valida la contraseña (la valida Keycloak):
// - los campos vacíos se avisan al instante, sin enviar nada al servidor;
// - el error se muestra en el aviso general de arriba y se limpia al escribir;
// - al enviar, el botón queda ocupado para que no se mande dos veces.
const formulario = document.getElementById("kc-form-login");

if (formulario) {
  const aviso = document.getElementById("sotra-aviso-login");
  const textoAviso = document.getElementById("sotra-aviso-login-texto");
  const boton = document.getElementById("kc-login");
  const campos = [document.getElementById("username"), document.getElementById("password")].filter(Boolean);

  const marcarCampos = (invalido) =>
    campos.forEach((campo) => (invalido ? campo.setAttribute("aria-invalid", "true") : campo.removeAttribute("aria-invalid")));

  const mostrarAviso = (texto) => {
    textoAviso.textContent = texto;
    aviso.hidden = false;
  };

  const limpiarAviso = () => {
    aviso.hidden = true;
    textoAviso.textContent = "";
    marcarCampos(false);
  };

  campos.forEach((campo) => campo.addEventListener("input", limpiarAviso));

  formulario.addEventListener("submit", (evento) => {
    const vacio = campos.find((campo) => campo.value.trim() === "");
    if (vacio) {
      evento.preventDefault();
      mostrarAviso(boton.dataset.avisoVacio);
      marcarCampos(true);
      vacio.focus();
      return;
    }
    boton.disabled = true;
    boton.value = boton.dataset.textoOcupado;
  });
}
