// Shapes of the API's JSON bodies (README API table); timestamps are ISO-8601 UTC strings.
export interface Product {
  code: string
  description: string
  createdAt: string
}

export interface StockLevel {
  productCode: string
  warehouseCode: string
  quantity: number
  updatedAt: string
}

// RFC 9457 Problem Details with the API's extensions (ADR-004).
export interface ProblemDetails {
  status: number
  code: string
  detail: string
  instance?: string
  traceId?: string
  errors?: Record<string, string[]>
}
