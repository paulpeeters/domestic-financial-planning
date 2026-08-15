ALTER TABLE payment_executions
ADD INDEX ix_payment_executions_dedupe (user_id, source_reference(128), execution_date, amount, description(128));
