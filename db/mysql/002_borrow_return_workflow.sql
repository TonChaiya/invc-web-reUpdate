-- INVC Web — application-owned MySQL database `invc_web` — migration 002: Borrow return workflow (append-only audit trail).
-- Target: MySQL 8.x database `invc_web` ONLY. Never executed against SQL Server INV. Applied by scripts/mysql-migrate.ps1.
-- Idempotent (IF NOT EXISTS), never drops, never alters migration 001 objects.
--
-- borrow_return_event: one row per RETURN (quantity_delta > 0) or CORRECTION (quantity_delta < 0, corrects_event_id set).
-- ReturnedQty of an item = SUM(quantity_delta) over its events; OutstandingQty = source QTY_ORDER − ReturnedQty (derived, never stored).
-- Deliberately NO foreign key to borrow_source_item / borrow_source_bill: the mirror is reconciled (rows may be deleted or
-- re-created when INV changes) and history must survive that. Snapshot columns (receive_no, working_code, facility_code,
-- drug_name, facility_name) keep the trail readable even if the source row disappears.
-- quantity_delta uses the same DECIMAL(14,0) as borrow_source_item.qty_order (live INV: all type-09 quantities are integers).

CREATE TABLE IF NOT EXISTS borrow_return_event (
    id                          BIGINT         NOT NULL AUTO_INCREMENT PRIMARY KEY,
    source_item_record_number   INT            NOT NULL,
    source_bill_record_number   INT            NOT NULL,
    receive_no                  VARCHAR(20)    NOT NULL,
    working_code                VARCHAR(14)    NOT NULL,
    drug_name                   VARCHAR(255)   NULL,
    facility_code               VARCHAR(12)    NULL,
    facility_name               VARCHAR(255)   NULL,
    event_type                  VARCHAR(12)    NOT NULL,          -- 'RETURN' | 'CORRECTION'
    quantity_delta              DECIMAL(14,0)  NOT NULL,          -- > 0 for RETURN, < 0 for CORRECTION
    event_at                    DATETIME       NOT NULL,          -- when the return physically happened (server default = now)
    actor                       VARCHAR(128)   NOT NULL,          -- authenticated identity or documented server-side fallback
    note                        VARCHAR(500)   NULL,              -- optional for RETURN, required for CORRECTION (reason)
    corrects_event_id           BIGINT         NULL,              -- CORRECTION → the RETURN event it reverses (part of)
    client_request_id           CHAR(36)       NULL,              -- browser form token: makes a double-submit idempotent
    created_at                  DATETIME       NOT NULL,
    UNIQUE KEY ux_borrow_return_event_client_request (client_request_id),
    KEY ix_borrow_return_event_item     (source_item_record_number, event_at),
    KEY ix_borrow_return_event_bill     (source_bill_record_number),
    KEY ix_borrow_return_event_facility (facility_code, event_at),
    KEY ix_borrow_return_event_code     (working_code),
    KEY ix_borrow_return_event_corrects (corrects_event_id),
    KEY ix_borrow_return_event_at       (event_at),
    KEY ix_borrow_return_event_actor    (actor),
    CONSTRAINT ck_borrow_return_event_type  CHECK (event_type IN ('RETURN', 'CORRECTION')),
    CONSTRAINT ck_borrow_return_event_delta CHECK ((event_type = 'RETURN' AND quantity_delta > 0 AND corrects_event_id IS NULL)
                                                OR (event_type = 'CORRECTION' AND quantity_delta < 0 AND corrects_event_id IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO schema_version (version, description, applied_at)
SELECT 2, 'borrow return workflow: borrow_return_event (append-only RETURN / CORRECTION trail)', NOW()
WHERE NOT EXISTS (SELECT 1 FROM schema_version WHERE version = 2);
