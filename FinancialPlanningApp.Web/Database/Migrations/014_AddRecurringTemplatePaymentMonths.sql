ALTER TABLE recurring_payment_templates
ADD COLUMN payment_months VARCHAR(50) NULL AFTER payment_month;
