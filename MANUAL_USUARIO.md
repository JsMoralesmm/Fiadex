# Manual de Usuario — Fiadex

**Versión 1.1 — 25/11/2026**

## 1. Introducción

Fiadex es una aplicación de escritorio para Windows que permite registrar **personas**, **préstamos (fiados)** y **abonos**, calcular saldos pendientes y consultar el historial por cliente. Funciona sin conexión y guarda la información en el equipo, en una base de datos SQLite.

Este manual está dirigido al usuario que administra la cartera: alta de clientes, registro de créditos y cobros, consulta de saldos y uso del asistente de IA.

---

## 2. Requisitos para usar la aplicación

- Windows de 64 bits.
- No es necesario instalar .NET: la versión portable ya incluye todo lo que necesita.
- Opcional: Ollama en `http://localhost:11434` con el modelo `qwen2.5:3b` si deseas usar el **Asistente IA**. Sin Ollama, el resto del programa funciona normalmente.

---

## 3. Acceso al sistema

### 3.1 Primera vez: crear la cuenta administradora

1. Ejecuta `Fiadex.exe`.
2. En la pantalla de acceso verás el subtítulo **“Configure la cuenta administradora”** y el botón **“Crear cuenta”**.
3. El campo **Usuario** viene precargado con `admin`; puedes conservarlo o cambiarlo.
4. Escribe una contraseña que cumpla:
   - Mínimo **12 caracteres**.
   - Al menos una **mayúscula**.
   - Al menos una **minúscula**.
   - Al menos un **número**.
   - Al menos un **símbolo**.
5. Pulsa **Crear cuenta** para entrar.

> Si la contraseña no cumple la política, aparecerá el mensaje: *“La contraseña debe tener 12 caracteres, mayúscula, minúscula, número y símbolo.”*

### 3.2 Iniciar sesión

1. Escribe tu **Usuario**.
2. Escribe tu **Contraseña**.
3. Pulsa **Iniciar sesión** o simplemente **Enter** desde cualquiera de los dos campos.

El campo de contraseña tiene un **icono de ojo** a la derecha:

- Al pulsarlo, la contraseña se muestra en texto plano y el ojo aparece tachado.
- Al volver a pulsarlo, la contraseña vuelve a ocultarse.
- El texto no se pierde al mostrarlo u ocultarlo.

### 3.3 Contraseña olvidada o cuenta bloqueada

Después de **5 intentos fallidos**, la cuenta se bloquea. El botón **Desbloquear cuenta** aparece solo si el usuario existe y está realmente bloqueado (un error de contraseña normal no lo muestra):

1. Escribe tu **Usuario**.
2. Escribe tu **Contraseña actual** en el campo de contraseña.
3. Pulsa **Desbloquear cuenta**.

Si la contraseña es correcta, el sistema reinicia el contador de intentos y permite volver a entrar; de lo contrario verás *“No se pudo desbloquear. Verifique el usuario y la contraseña.”*

El desbloqueo es una función local del equipo; no hay correo ni verificación en dos pasos, y no se puede hacer solo con el botón: **siempre se exige la contraseña**.

> **Si olvidaste la contraseña**, la app no puede recuperarla por seguridad. Pide al administrador del equipo que ejecute la herramienta local de rescate (`Herramientas\RestablecerContrasena.ps1`); restablece la contraseña y desbloquea la cuenta **sin borrar los datos**. El procedimiento está en el *Manual de Despliegue*, apartado «Recuperación de contraseña olvidada» de la sección 9.

### 3.4 Cambiar la contraseña

Dentro de la aplicación:

1. Abre el menú de usuario en la barra lateral y pulsa **Cambiar contraseña**.
2. Completa:
   - **Contraseña actual**.
   - **Nueva contraseña**.
   - **Repita la nueva contraseña**.
3. Cada campo tiene su propio **ojo** para mostrar u ocultar el texto.
4. Pulsa **Guardar nueva contraseña**.

Si las nuevas contraseñas no coinciden verás: *“Las contraseñas nuevas no coinciden.”*

### 3.5 Cerrar sesión

En la barra lateral pulsa **Cerrar sesión**. La sesión también finaliza automáticamente:

- A los **8 horas** de haber iniciado sesión, aunque sigas trabajando (el límite es estricto), o
- Después de **15 minutos sin actividad** (movimiento del ratón o pulsaciones de tecla).

Un minuto antes de cerrarse por inactividad aparece un aviso con las opciones **Continuar** y **Cerrar sesión**. Si eliges **Continuar**, el contador de inactividad vuelve a empezar, pero el límite de 8 horas no se reinicia.

---

## 4. Navegación general

La barra lateral tiene siete módulos:

| Módulo | Para qué sirve |
|---|---|
| **Dashboard** | Indicadores y gráficas del estado de la cartera. |
| **Personas** | Alta, búsqueda, edición y eliminación de clientes. |
| **Préstamos** | Registro y control de créditos. |
| **Historial** | Consulta del detalle de un cliente seleccionado. |
| **Abonos** | Registro y control de pagos. |
| **Comprobantes** | Descarga de recibos de préstamos saldados. |
| **Asistente IA** | Preguntas sobre los datos locales. |

### Barra lateral colapsable

- Pulsa el botón de colapso para dejar solo los **iconos**, centrados y organizados.
- Pulsa el icono de **alfiler** para fijar o desfijar la barra.
- Pasa el ratón sobre un icono para ver su nombre como ayuda (tooltip).

### Costumbre importante: editar solo con botones

Hacer **doble clic** en una fila de cualquier tabla **no abre nada y no modifica nada**. Para editar, selecciona la fila y pulsa el botón correspondiente:

- **Editar persona**
- **Editar préstamo**
- **Editar abono**

Los cuadros de edición tienen un pie fijo con **Cancelar** y **Guardar cambios**, por lo que los botones nunca quedan cortados aunque la ventana se haga más pequeña.

---

## 5. Dashboard

Al entrar verás el **Resumen Financiero**:

| Indicador | Significado |
|---|---|
| **Saldo pendiente** | Total que aún deben los clientes. |
| **Abonos del mes** | Lo cobrado en el mes vigente. |
| **Préstamos activos** | Cuentas con saldo mayor que cero. |
| **Abonos registrados** | Cantidad total de pagos históricos. |
| **Mediana de préstamos** | Valor típico de los créditos. |

También verás dos gráficas de los últimos 12 meses, **de izquierda a derecha y de más antiguo a más reciente**:

- **Abonos por mes**, barras con el mes en español y el importe al pasar el ratón.
- **Tendencia de pagos**, línea con etiqueta de mes en el eje, escala horizontal y un punto por mes.

Pulsa **Actualizar datos** para refrescar los indicadores después de registrar movimientos.

---

## 6. Personas

### Registrar una persona

1. Entra a **Personas**.
2. Escribe el **Nombre completo** (obligatorio).
3. Escribe el **Teléfono / Contacto** (opcional).
4. Pulsa **Registrar persona**.

Verás el mensaje *“Persona registrada.”* y la persona aparecerá en la tabla.

### Buscar una persona

En el campo **Buscar**, escribe parte del nombre o del teléfono:

- La tabla se filtra en tiempo real.
- Arriba se muestra **“1 resultado”** o **“X resultados”**.
- Pulsa **Limpiar** para ver todas las personas nuevamente.

### Editar o eliminar

1. Selecciona una fila de la tabla.
2. Pulsa **Editar persona** o **Eliminar persona**.

> Eliminar una persona también elimina su préstamo y todos sus abonos. Hazlo solo si estás seguro.

---

## 7. Préstamos

### Registrar un préstamo

1. Entra a **Préstamos**.
2. En el panel izquierdo:
   - Busca y elige la **Persona**: el cuadro queda limpio para buscar otra, pero debajo aparece el indicador **Cliente:** con el nombre elegido (y una marca verde cuando hay selección).
   - Escribe el **Monto a prestar** (mayor que cero).
   - Elige la **Fecha del crédito** (no puede ser futura).
   - Agrega **Notas u observaciones** (opcional).
3. Pulsa **Registrar préstamo**.

### Una sola cuenta por persona

Si la persona ya tenía un préstamo, **no se crea otra fila**: el nuevo monto se acumula en su cuenta existente.

- El monto total se suma.
- Se conserva la fecha más antigua.
- En las notas se agrega el texto: `Préstamo adicional de … registrado el dd/MM/yyyy`.
- El mensaje indica: *“Préstamo acumulado a la cuenta existente. Aún debe $….”*

### Estado de la deuda

La tabla de préstamos muestra:

- **Monto**, **Abonado**, **Saldo** y **Estado**.
- El estado dice **“Aún debe $…”** mientras haya saldo, o **“ya está pagado”** cuando el saldo llegue a cero.

### Reglas de fecha y monto

| Situación | Resultado |
|---|---|
| Fecha futura | *“La fecha del préstamo no puede ser futura.”* |
| Monto cero o negativo | *“El monto del préstamo debe ser mayor que cero.”* |
| Sin persona seleccionada | *“Seleccione una persona.”* |
| Editar con abonos: monto menor a lo ya abonado | *“El monto no puede ser menor que los abonos registrados (…).”* |
| Editar fecha posterior al abono más antiguo | *“La fecha del préstamo no puede ser posterior al abono más antiguo (dd/MM/yyyy).”* |

### Editar o eliminar

Selecciona la fila y pulsa **Editar préstamo** o **Eliminar préstamo**. Eliminar un préstamo también elimina sus abonos.

---

## 8. Abonos

### Registrar un abono

1. Entra a **Abonos**.
2. Selecciona el **Préstamo pendiente**. El cuadro muestra el nombre del cliente y su saldo; debajo, el indicador **Cliente:** confirma la persona elegida con una marca verde. Solo aparecen las cuentas con saldo mayor que cero: al saldarse, el préstamo se retira automáticamente de este selector.
3. Escribe el **Monto abonado**.
4. Elige la **Fecha del pago**. El calendario solo permite fechas entre la fecha del préstamo y hoy.
5. Agrega **Notas** (opcional).
6. Pulsa **Registrar abono**.

Después de guardar, el mensaje indica el saldo restante, por ejemplo:

- *“Abono registrado. Aún debe $12,000.”*
- *“Abono registrado. El préstamo ya está pagado.”*

### Comprobante de préstamo saldado

Cuando un abono deja el saldo en cero, el aviso superior ofrece el botón **Descargar recibo**. Para volver a emitir un PDF (incluidos los préstamos saldados con anterioridad) entra en la sección **Comprobantes**, que está justo después de **Abonos** en el menú lateral.

### Reglas importantes

| Situación | Resultado |
|---|---|
| Monto mayor al saldo | *“El abono excede el saldo pendiente de ….”* |
| Fecha anterior al préstamo | *“La fecha del abono no puede ser anterior a la fecha del préstamo (dd/MM/yyyy).”* |
| Fecha futura | *“La fecha del abono no puede ser futura.”* |
| Sin préstamo seleccionado | *“Seleccione un préstamo.”* |

### Editar o eliminar

Selecciona un abono en la tabla y pulsa **Editar abono** o **Eliminar abono**. Al editar se recalcula el saldo automáticamente.

---

## 9. Historial por cliente

La pestaña **Historial** muestra toda la información de **un cliente a la vez**:

1. En **Buscar cliente en historial**, escribe parte del nombre o del teléfono.
2. Elige el cliente en la lista desplegable. El cuadro vuelve a quedar vacío para buscar otro, pero el indicador **Cliente:** y el resumen siguen mostrando la persona elegida.
3. Se actualizan automáticamente:
   - **Resumen**: cliente, préstamos, prestado, pagado, saldo y estado.
   - **Préstamos del cliente seleccionado**: fecha, monto, abonado, saldo, estado y notas.
   - **Abonos del cliente seleccionado**: préstamo, fecha, monto y notas.
4. Pulsa **Limpiar** para volver a ver todos los clientes.

Si no hay cliente elegido, verás el mensaje *“Seleccione un cliente para ver su historial.”*

---

## 10. Comprobantes

Esta sección está separada de **Abonos** para no mezclar cuentas pendientes con recibos:

1. Entra a **Comprobantes** en el menú lateral.
2. Elige un **préstamo saldado** de la lista (muestra el cliente y la referencia de seis dígitos).
3. Pulsa **Descargar recibo**.

Si todavía no hay préstamos saldados, verás el mensaje *“Aún no hay préstamos saldados”* y la lista no aparece.

El PDF es monocromático y en tamaño A4. Incluye los datos del cliente, la referencia del préstamo con seis dígitos (`000006`), la tabla con todos los abonos, el total abonado, el saldo final, el número de comprobante (`FIA-000006-20261123`) y la fecha de emisión. Se genera en el equipo, sin conexión, y se guarda en la ubicación que elijas con el nombre `Recibo_Nombre_yyyyMMdd.pdf`.

---

## 11. Asistente IA

En la pestaña **Asistente IA** puedes preguntar por préstamos, abonos, saldos e historial:

- Escribe tu pregunta y pulsa **Enviar** o **Enter**; usa **Shift+Enter** para un salto de línea.
- Pulsa **Resumen** para obtener un resumen general.
- Pulsa **Limpiar conversación** para reiniciar el chat.

El asistente:

- Solo **consulta** información local; no modifica datos. Consulta los datos de esta aplicación: **no compartas información sensible**.
- Usa un modelo local de Ollama (`qwen2.5:3b`) en `http://localhost:11434`. Por seguridad solo se conecta a Ollama en el propio equipo o a servidores HTTPS; no envíes datos a direcciones arbitrarias.
- Valida las cifras contra la base de datos: si el modelo inventa un número, se muestra un aviso.
- Si Ollama no está disponible o hay un error, responde de forma genérica: *“No se pudo consultar el asistente. Verifique que Ollama esté disponible.”*

La conversación se guarda solo mientras dure la sesión.

---

## 12. Apariencia y accesibilidad

- La aplicación se abre con el tema **Negro** por defecto: una interfaz oscura monocromática, con contraste alto y tarjetas elevadas.
- Desde **Accesibilidad** puedes cambiar el tema, el tamaño del texto, el contraste y las animaciones sin perder funcionalidad.
- Los avisos flotantes y las ventanas de edición o confirmación siguen el mismo diseño oscuro: cabecera, acciones separadas y cierre visible. El foco va al botón **Guardar/Confirmar**, y si amplías el texto las ventanas se desplazan en vez de recortarse.
- El foco está activado por defecto con el nivel **Visible** (contorno de 2,5 px) para que siempre se vea dónde estás. El escalado de texto es estable: subir y bajar el tamaño vuelve exactamente al original.
- Las fechas se eligen en un calendario desplegable del mismo estilo. La cabecera tiene selectores de **Mes** y **Año** para saltos rápidos, y la barra inferior ofrece **Hoy**, **Cancelar** y **Aceptar**:
  - **Hoy** coloca la fecha actual (respetando los límites permitidos).
  - **Cancelar** descarta los cambios y restaura la fecha que había al abrir el calendario.
  - **Aceptar** confirma la fecha y cierra el calendario.
  - Las flechas de navegación del calendario se han retirado porque causaban fallos; para cambiar de mes o año usa los selectores **Mes** y **Año**. Los nombres para lectores de pantalla siguen funcionando igual.
- En las tablas de **Personas**, **Préstamos** y **Abonos** puedes seleccionar una fila y pulsar **Supr** para eliminarla; aparece la misma confirmación que con el botón **Eliminar**. Si estás editando una celda, **Supr** sigue borrando texto.
- Los préstamos saldados no aparecen en el selector principal de **Abonos**; se listan aparte en **Comprobantes** para descargar su recibo en PDF.
- Tras elegir un cliente en **Préstamos** o **Historial**, el cuadro de búsqueda queda limpio para buscar otro; la persona elegida sigue visible en la etiqueta del formulario o en el resumen del historial.

---

## 13. Buenas prácticas

- Registra siempre el **teléfono** y las **notas**: así el asistente IA tiene más contexto y no reporta datos incompletos.
- Usa el **ojo** de la contraseña solo cuando estés seguro de que nadie está mirando la pantalla.
- Cierra sesión o deja que expire si compartes el equipo.
- Haz copias periódicas de la base de datos (ver el **Manual de Despliegue**).
- No modifiques directamente el archivo `fiadex.db` mientras la aplicación esté abierta.

---

## 14. Solución rápida de problemas

| Síntoma | Qué hacer |
|---|---|
| La aplicación no abre | Comprueba que ejecutas `Fiadex.exe` desde una carpeta extraída completa, no desde dentro del ZIP. |
| No aparecen los cambios | Cierra la versión anterior y vuelve a extraer el ZIP actual. |
| No puedo iniciar sesión | Usa **Desbloquear cuenta** tras 5 intentos fallidos. |
| El asistente no responde | Instala Ollama, ejecútalo y descarga el modelo con `ollama pull qwen2.5:3b`. |
| El ojo no muestra la contraseña | Asegúrate de estar usando la versión actual del ZIP. |
| Los datos “desaparecieron” | La base está en `%LOCALAPPDATA%\Fiadex\fiadex.db`, no junto al exe. Verifica que no se haya borrado ese archivo. |
