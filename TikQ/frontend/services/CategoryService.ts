import { apiRequest } from "@/lib/api-client"
import type { ApiCategoryResponse } from "@/lib/api-types"
import type { CategoriesData, Category, SubIssue } from "@/services/categories-types"

export interface CategoryService {
  list(): Promise<CategoriesData>
  saveAll(data: CategoriesData): Promise<void>
  seedIfEmpty?(seed: CategoriesData): Promise<void>
}

const STORAGE_KEY = "ticketing.categories.v1"

const slugify = (value: string) =>
  value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9\u0600-\u06ff\s-]/g, "")
    .replace(/\s+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "") || "category"

const buildRemoteCategories = (apiCategories: ApiCategoryResponse[], overrides: CategoriesData): CategoriesData => {
  const result: CategoriesData = {}

  for (const apiCategory of apiCategories) {
    const slug = slugify(apiCategory.name)
    const override = overrides[slug]
    const nextCategory: Category = {
      id: override?.id ?? slug,
      backendId: apiCategory.id,
      label: override?.label ?? apiCategory.name,
      description: override?.description ?? apiCategory.description ?? undefined,
      subIssues: {},
    }

    for (const sub of apiCategory.subcategories) {
      const subSlug = slugify(sub.name)
      const overrideSub = override?.subIssues?.[subSlug]
      const nextSub: SubIssue = {
        id: overrideSub?.id ?? subSlug,
        backendId: sub.id,
        label: overrideSub?.label ?? sub.name,
        description: overrideSub?.description,
        fields: overrideSub?.fields ?? [],
      }
      nextCategory.subIssues[subSlug] = nextSub
    }

    result[slug] = nextCategory
  }

  return result
}

const readLocalOverrides = (): CategoriesData => {
  if (typeof window === "undefined") return {}
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) {
      return {}
    }
    return JSON.parse(raw)
  } catch {
    return {}
  }
}

export class RemoteCategoryService implements CategoryService {
  async list(): Promise<CategoriesData> {
    const overrides = readLocalOverrides()
    try {
      const apiCategories = await apiRequest<ApiCategoryResponse[]>("/api/categories")
      return buildRemoteCategories(apiCategories, overrides)
    } catch (error) {
      console.error("Failed to load categories from backend", error)
      return overrides
    }
  }

  async saveAll(data: CategoriesData): Promise<void> {
    if (typeof window === "undefined") return
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data))
  }

  async seedIfEmpty(seed: CategoriesData): Promise<void> {
    if (typeof window === "undefined") return
    const existing = localStorage.getItem(STORAGE_KEY)
    if (!existing) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(seed))
    }
  }
}

export const categoryService: CategoryService = new RemoteCategoryService()
