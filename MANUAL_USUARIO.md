# Manual de Usuario — Fiadex

**Versión 1.0 — 19/11/2026**

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
3. Pulsa **Iniciar sesión**.

El campo de contraseña tiene un **icono de ojo** a la derecha:

- Al pulsarlo, la contraseña se muestra en texto plano y el ojo aparece tachado.
- Al volver a pulsarlo, la contraseña vuelve a ocultarse.
- El texto no se pierde al mostrarlo u ocultarlo.

### 3.3 Contraseña olvidada o cuenta bloqueada

Después de **5 intentos fallidos**, la cuenta se bloquea. En la pantalla de acceso aparece el botón **Desbloquear cuenta**:

1. Pulsa **Desbloquear cuenta**.
2. Confirma la recuperación como administrador local.
3. El sistema reinicia el contador de intentos y permite volver a entrar.

El desbloqueo es una función local del equipo; no hay correo ni verificación en dos pasos.

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

- A los **8 horas** de haber iniciado sesión, o
- Después de **15 minutos sin actividad** (movimiento del ratón o pulsaciones de tecla).

---

## 4. Navegación general

La barra lateral tiene seis módulos:

| Módulo | Para qué sirve |
|---|---|
| **Dashboard** | Indicadores y gráficas del estado de la cartera. |
| **Personas** | Alta, búsqueda, edición y eliminación de clientes. |
| **Préstamos** | Registro y control de créditos. |
| **Historial** | Consulta del detalle de un cliente seleccionado. |
| **Abonos** | Registro y control de pagos. |
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
| **Errores registrados** | Intentos de operaciones inválidas en la sesión. |
| **Campos incompletos** | Personas sin teléfono o movimientos sin notas. |

También verás:

- **Frecuencia de uso por módulo**, con contadores de cada sección visitada.
- **Abonos por mes**, gráfica de barras de los últimos 12 meses.
- **Tendencia de pagos**, gráfica de línea.

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
   - Selecciona la **Persona**.
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
2. Selecciona el **Préstamo pendiente**. El cuadro muestra el nombre del cliente y su saldo.
3. Escribe el **Monto abonado**.
4. Elige la **Fecha del pago**. El calendario solo permite fechas entre la fecha del préstamo y hoy.
5. Agrega **Notas** (opcional).
6. Pulsa **Registrar abono**.

Después de guardar, el mensaje indica el saldo restante, por ejemplo:

- *“Abono registrado. Aún debe $12,000.”*
- *“Abono registrado. El préstamo ya está pagado.”*

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
2. Elige el cliente en la lista desplegable.
3. Se actualizan automáticamente:
   - **Resumen**: cliente, préstamos, prestado, pagado, saldo y estado.
   - **Préstamos del cliente seleccionado**: fecha, monto, abonado, saldo, estado y notas.
   - **Abonos del cliente seleccionado**: préstamo, fecha, monto y notas.
4. Pulsa **Limpiar** para volver a ver todos los clientes.

Si no hay cliente elegido, verás el mensaje *“Seleccione un cliente para ver su historial.”*

---

## 10. Asistente IA

En la pestaña **Asistente IA** puedes preguntar por préstamos, abonos, saldos e historial:

- Escribe tu pregunta y pulsa **Enviar**.
- Pulsa **Resumen** para obtener un resumen general.
- Pulsa **Limpiar conversación** para reiniciar el chat.

El asistente:

- Solo **consulta** información local; no modifica datos.
- Usa un modelo local de Ollama (`qwen2.5:3b`) en `http://localhost:11434`.
- Valida las cifras contra la base de datos: si el modelo inventa un número, se muestra un aviso.
- Si Ollama no está disponible, responde: *“Ollama no está disponible. Instale Ollama, ejecute el servicio y configure qwen2.5:3b.”*

La conversación se guarda solo mientras dure la sesión.

---

## 11. Buenas prácticas

- Registra siempre el **teléfono** y las **notas**: así baja el indicador *Campos incompletos*.
- Usa el **ojo** de la contraseña solo cuando estés seguro de que nadie está mirando la pantalla.
- Cierra sesión o deja que expire si compartes el equipo.
- Haz copias periódicas de la base de datos (ver el **Manual de Despliegue**).
- No modifiques directamente el archivo `fiadex.db` mientras la aplicación esté abierta.

---

## 12. Solución rápida de problemas

| Síntoma | Qué hacer |
|---|---|
| La aplicación no abre | Comprueba que ejecutas `Fiadex.exe` desde una carpeta extraída completa, no desde dentro del ZIP. |
| No aparecen los cambios | Cierra la versión anterior y vuelve a extraer el ZIP actual. |
| No puedo iniciar sesión | Usa **Desbloquear cuenta** tras 5 intentos fallidos. |
| El asistente no responde | Instala Ollama, ejecútalo y descarga el modelo con `ollama pull qwen2.5:3b`. |
| El ojo no muestra la contraseña | Asegúrate de estar usando la versión actual del ZIP. |
| Los datos “desaparecieron” | La base está en `%LOCALAPPDATA%\Fiadex\fiadex.db`, no junto al exe. Verifica que no se haya borrado ese archivo. |
