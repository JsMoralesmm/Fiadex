# Sugerencias de mejora — Fiadex

Guardadas el 18/11/2026, antes de realizar nuevos cambios o arreglos.

## Prioridad alta

1. **Backups automáticos**
   - Copia diaria de la base SQLite.
   - Opción de exportar/importar desde la configuración.
   - Ideal para no perder la cartera por un problema del equipo.

2. **Fecha de vencimiento por préstamo**
   - Hoy solo se maneja la fecha de crédito.
   - Agregando `Fecha de vencimiento` se podrían mostrar alertas de:
     - Préstamo al día.
     - Próximo a vencer.
     - Vencido.

3. **Auditoría de cambios**
   - Registrar quién y cuándo cambió un monto, una fecha o un abono.
   - Muy útil aunque hoy sea un sistema de un solo usuario.

4. **Eliminación lógica**
   - En vez de borrar préstamos/abonos, marcarlos como inactivos.
   - Permite conservar el historial y evitar pérdidas accidentales.

## Prioridad media

5. **Recibos de pago**
   - Generar comprobante PDF o imagen por cada abono.
   - Podría incluir nombre, préstamo, fecha, monto y saldo restante.

6. **Filtros y búsqueda global**
   - Buscar por nombre, teléfono, préstamo o rango de fechas.
   - Filtrar la tabla de préstamos por `Pagado` / `Pendiente`.

7. **Recordatorios visuales en el Dashboard**
   - Clientes con saldo más antiguo.
   - Préstamos sin abonos en X días.
   - Abonos registrados este mes.

8. **Atajos de teclado**
   - `Enter` para guardar.
   - `Esc` para cancelar.
   - Doble clic ya implementado para préstamos y abonos.

## Prioridad baja / futura

9. **Roles de usuario**
   - Administrador: edita y elimina.
   - Consultor: solo lectura.

10. **Cifrado de la base local**
    - Proteger la información sensible si el portable se usa en equipos compartidos.

11. **Instalador**
    - Mantener el ZIP portable y agregar un instalador opcional con acceso directo y desinstalación limpia.

12. **Reportes más completos**
    - Cartera por cliente.
    - Ingresos por mes.
    - Antigüedad de deudas.
    - Exportación a Excel/CSV.
