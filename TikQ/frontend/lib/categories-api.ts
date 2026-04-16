import { apiRequest } from "./api-client"
import type {
  ApiCategoryResponse,
  ApiCategoryListResponse,
  ApiCategoryRequest,
  ApiSubcategoryResponse,
  ApiSubcategoryRequest,
} from "./api-types"

/**
 * Get all categories (public endpoint - only active)
 */
export async function getAllCategories(token: string | null = null): Promise<ApiCategoryResponse[]> {
  return apiRequest<ApiCategoryResponse[]>("/api/categories", {
    method: "GET",
    token: token || undefined,
  })
}

/**
 * Get admin categories with search and pagination
 */
export async function getAdminCategories(
  token: string | null,
  options?: { search?: string; page?: number; pageSize?: number }
): Promise<ApiCategoryListResponse> {
  const params = new URLSearchParams()
  if (options?.search) params.append("search", options.search)
  if (options?.page) params.append("page", options.page.toString())
  if (options?.pageSize) params.append("pageSize", options.pageSize.toString())

  const query = params.toString()
  const url = `/api/categories/admin${query ? `?${query}` : ""}`

  return apiRequest<ApiCategoryListResponse>(url, {
    method: "GET",
    token,
  })
}

/**
 * Create a new category (Admin only)
 */
export async function createCategory(
  token: string | null,
  category: ApiCategoryRequest
): Promise<ApiCategoryResponse> {
  return apiRequest<ApiCategoryResponse>("/api/categories", {
    method: "POST",
    token,
    body: category,
  })
}

/**
 * Update category (Admin only)
 */
export async function updateCategory(
  token: string | null,
  id: number,
  category: ApiCategoryRequest
): Promise<ApiCategoryResponse> {
  return apiRequest<ApiCategoryResponse>(`/api/categories/${id}`, {
    method: "PUT",
    token,
    body: category,
  })
}

/**
 * Delete category (Admin only)
 */
export async function deleteCategory(token: string | null, id: number): Promise<void> {
  await apiRequest(`/api/categories/${id}`, {
    method: "DELETE",
    token,
  })
}

/**
 * Get subcategories for a category (Admin only)
 */
export async function getSubcategories(
  token: string | null,
  categoryId: number
): Promise<ApiSubcategoryResponse[]> {
  return apiRequest<ApiSubcategoryResponse[]>(`/api/categories/${categoryId}/subcategories`, {
    method: "GET",
    token,
  })
}

/**
 * Create a new subcategory (Admin only)
 */
export async function createSubcategory(
  token: string | null,
  categoryId: number,
  subcategory: ApiSubcategoryRequest
): Promise<ApiSubcategoryResponse> {
  return apiRequest<ApiSubcategoryResponse>(`/api/categories/${categoryId}/subcategories`, {
    method: "POST",
    token,
    body: subcategory,
  })
}

/**
 * Update subcategory (Admin only)
 */
export async function updateSubcategory(
  token: string | null,
  id: number,
  subcategory: ApiSubcategoryRequest
): Promise<ApiSubcategoryResponse> {
  return apiRequest<ApiSubcategoryResponse>(`/api/categories/subcategories/${id}`, {
    method: "PUT",
    token,
    body: subcategory,
  })
}

/**
 * Delete subcategory (Admin only)
 */
export async function deleteSubcategory(token: string | null, id: number): Promise<void> {
  await apiRequest(`/api/categories/subcategories/${id}`, {
    method: "DELETE",
    token,
  })
}

