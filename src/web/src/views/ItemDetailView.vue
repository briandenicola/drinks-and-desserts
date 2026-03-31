<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useItemsStore } from '../stores/items'
import { itemsApi, type Item } from '../services/items'

const route = useRoute()
const router = useRouter()
const itemsStore = useItemsStore()

const item = ref<Item | null>(null)
const isEditing = ref(false)
const isSaving = ref(false)
const showDeleteConfirm = ref(false)
const editRating = ref(0)
const editNotes = ref('')
const editName = ref('')
const editBrand = ref('')
const editCategory = ref('')
const editVenueName = ref('')
const editVenueAddress = ref('')
const editTags = ref<string[]>([])
const newTag = ref('')

onMounted(async () => {
  const { data } = await itemsApi.get(route.params.id as string)
  item.value = data
  resetEditFields(data)
})

function resetEditFields(data: Item) {
  editRating.value = data.userRating ?? 0
  editNotes.value = data.userNotes ?? ''
  editName.value = data.name ?? ''
  editBrand.value = data.brand ?? ''
  editCategory.value = data.category ?? ''
  editVenueName.value = data.venue?.name ?? ''
  editVenueAddress.value = data.venue?.address ?? ''
  editTags.value = [...(data.tags ?? [])]
}

function startEditing() {
  if (item.value) resetEditFields(item.value)
  isEditing.value = true
}

function addTag() {
  const tag = newTag.value.trim().toLowerCase()
  if (tag && !editTags.value.includes(tag)) {
    editTags.value.push(tag)
  }
  newTag.value = ''
}

function removeTag(tag: string) {
  editTags.value = editTags.value.filter(t => t !== tag)
}

async function save() {
  if (!item.value) return
  isSaving.value = true
  try {
    const updated = await itemsStore.updateItem(item.value.id, {
      name: editName.value || undefined,
      brand: editBrand.value || undefined,
      category: editCategory.value || undefined,
      venue: editVenueName.value ? { name: editVenueName.value, address: editVenueAddress.value || undefined } : undefined,
      userRating: editRating.value || undefined,
      userNotes: editNotes.value || undefined,
      tags: editTags.value,
      status: 'reviewed',
    })
    item.value = updated
    isEditing.value = false
  } finally {
    isSaving.value = false
  }
}

async function deleteItem() {
  if (!item.value) return
  await itemsStore.deleteItem(item.value.id)
  router.push('/items')
}

function isAiGenerated(data: Item): boolean {
  return (data.aiConfidence != null && data.aiConfidence > 0)
}
</script>

<template>
  <div v-if="!item" class="p-4 text-stone-500 text-center py-12">Loading...</div>

  <div v-else class="p-4 max-w-lg mx-auto">
    <!-- Back -->
    <button @click="router.back()" class="text-stone-400 hover:text-stone-200 text-sm mb-4">
      ← Back
    </button>

    <!-- Photos -->
    <div v-if="item.photoUrls.length" class="mb-4 -mx-4">
      <div class="flex gap-2 overflow-x-auto px-4 pb-2">
        <img
          v-for="(url, i) in item.photoUrls"
          :key="i"
          :src="url"
          class="h-48 object-cover rounded-xl"
        />
      </div>
    </div>

    <!-- Header -->
    <div class="mb-4">
      <span class="text-xs px-2 py-0.5 rounded-full bg-stone-800 text-stone-400">{{ item.type }}</span>
      <span v-if="item.status === 'ai-draft'" class="text-xs text-amber-500 ml-2">AI Draft — tap edit to review</span>
      <h2 class="text-2xl font-bold mt-2">{{ item.name }}</h2>
      <p v-if="item.brand" class="text-stone-400">{{ item.brand }}</p>
      <p v-if="item.category" class="text-sm text-stone-500">{{ item.category }}</p>
    </div>

    <!-- AI Summary -->
    <div v-if="item.aiSummary" class="bg-stone-900 border border-stone-800 rounded-xl p-4 mb-4">
      <p v-if="isAiGenerated(item)" class="text-xs text-amber-500 mb-1">AI Agent Analysis</p>
      <p v-else class="text-xs text-stone-500 mb-1">Local Extraction (AI agents were not available)</p>
      <p class="text-sm text-stone-300">{{ item.aiSummary }}</p>
      <p v-if="item.aiConfidence" class="text-xs text-stone-600 mt-2">
        Confidence: {{ Math.round(item.aiConfidence * 100) }}%
      </p>
    </div>

    <!-- Venue -->
    <div v-if="item.venue" class="mb-4">
      <p class="text-sm text-stone-500">Location: {{ item.venue.name }}</p>
      <p v-if="item.venue.address" class="text-xs text-stone-600">{{ item.venue.address }}</p>
    </div>

    <!-- Workflow History Link -->
    <router-link
      v-if="item.captureId"
      :to="`/history/${item.captureId}`"
      class="inline-flex items-center gap-1.5 text-xs text-stone-500 hover:text-amber-500 transition-colors mb-4"
    >
      <span>🔍</span>
      <span>View processing history</span>
    </router-link>

    <!-- Rating & Notes (view/edit) -->
    <div v-if="!isEditing" class="space-y-3">
      <div v-if="item.userRating" class="text-amber-500 text-lg">
        {{ '★'.repeat(item.userRating) }}{{ '☆'.repeat(5 - item.userRating) }}
      </div>
      <p v-if="item.userNotes" class="text-stone-300 text-sm">{{ item.userNotes }}</p>

      <div class="flex gap-3 pt-4">
        <button
          @click="startEditing"
          class="flex-1 bg-amber-700 hover:bg-amber-600 text-white py-3 rounded-xl font-medium"
        >
          {{ item.status === 'ai-draft' ? 'Review & Rate' : 'Edit' }}
        </button>
        <button
          @click="showDeleteConfirm = true"
          class="px-4 bg-stone-800 hover:bg-stone-700 text-red-400 py-3 rounded-xl text-sm"
        >
          Delete
        </button>
      </div>
    </div>

    <!-- Edit mode -->
    <div v-else class="space-y-4">
      <!-- Name -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Name</label>
        <input
          v-model="editName"
          type="text"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 focus:outline-none focus:border-amber-700"
        />
      </div>

      <!-- Brand & Category row -->
      <div class="grid grid-cols-2 gap-3">
        <div>
          <label class="block text-sm text-stone-400 mb-1">Brand</label>
          <input
            v-model="editBrand"
            type="text"
            placeholder="e.g. Four Roses"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
          />
        </div>
        <div>
          <label class="block text-sm text-stone-400 mb-1">Category</label>
          <input
            v-model="editCategory"
            type="text"
            placeholder="e.g. Small Batch"
            class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
          />
        </div>
      </div>

      <!-- Location -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Location</label>
        <input
          v-model="editVenueName"
          type="text"
          placeholder="e.g. Rare Books Bar"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
        />
        <input
          v-model="editVenueAddress"
          type="text"
          placeholder="Address (optional)"
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700 mt-2"
        />
      </div>

      <!-- Star rating -->
      <div>
        <label class="block text-sm text-stone-400 mb-2">Rating</label>
        <div class="flex gap-1">
          <button
            v-for="star in 5"
            :key="star"
            @click="editRating = star"
            class="text-3xl transition-colors"
            :class="star <= editRating ? 'text-amber-500' : 'text-stone-700'"
          >★</button>
        </div>
      </div>

      <!-- Notes -->
      <div>
        <label class="block text-sm text-stone-400 mb-1">Your Notes</label>
        <textarea
          v-model="editNotes"
          rows="3"
          placeholder="What did you think? Flavor notes, pairing, occasion..."
          class="w-full bg-stone-800 border border-stone-700 rounded-xl px-4 py-3 text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700 resize-none"
        />
      </div>

      <!-- Tags -->
      <div>
        <label class="block text-sm text-stone-400 mb-2">Tags</label>
        <div class="flex flex-wrap gap-2 mb-2">
          <span
            v-for="tag in editTags"
            :key="tag"
            class="inline-flex items-center gap-1 text-sm bg-stone-800 text-stone-300 px-3 py-1 rounded-full"
          >
            {{ tag }}
            <button @click="removeTag(tag)" class="text-stone-500 hover:text-red-400 ml-1">×</button>
          </span>
        </div>
        <div class="flex gap-2">
          <input
            v-model="newTag"
            type="text"
            placeholder="Add a tag..."
            @keydown.enter.prevent="addTag"
            class="flex-1 bg-stone-800 border border-stone-700 rounded-xl px-4 py-2 text-sm text-stone-100 placeholder-stone-600 focus:outline-none focus:border-amber-700"
          />
          <button
            @click="addTag"
            :disabled="!newTag.trim()"
            class="px-4 bg-stone-800 hover:bg-stone-700 disabled:opacity-30 text-amber-500 rounded-xl text-sm font-medium"
          >+ Add</button>
        </div>
      </div>

      <div class="flex gap-3 pt-2">
        <button
          @click="save"
          :disabled="isSaving"
          class="flex-1 bg-amber-700 hover:bg-amber-600 disabled:bg-stone-700 text-white py-3 rounded-xl font-medium"
        >
          {{ isSaving ? 'Saving...' : 'Save' }}
        </button>
        <button @click="isEditing = false" class="px-6 bg-stone-800 hover:bg-stone-700 text-stone-300 py-3 rounded-xl">
          Cancel
        </button>
      </div>
    </div>

    <!-- Tags (read-only, outside edit mode) -->
    <div v-if="!isEditing && item.tags.length" class="mt-4 flex gap-2 flex-wrap">
      <span v-for="tag in item.tags" :key="tag" class="text-xs bg-stone-800 text-stone-400 px-2 py-1 rounded-full">
        {{ tag }}
      </span>
    </div>

    <!-- Delete Confirmation Modal -->
    <Teleport to="body">
      <div v-if="showDeleteConfirm" class="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4" @click.self="showDeleteConfirm = false">
        <div class="bg-stone-900 border border-stone-700 rounded-2xl p-6 w-full max-w-sm">
          <h3 class="text-lg font-semibold text-red-400 mb-4">Delete Item</h3>
          <p class="text-sm text-stone-300 mb-4">
            Are you sure you want to delete <strong>{{ item.name }}</strong>? This action cannot be undone.
          </p>
          <div class="flex gap-2 justify-end">
            <button @click="showDeleteConfirm = false" class="px-4 py-2 text-sm rounded-xl bg-stone-800 text-stone-400 hover:bg-stone-700">Cancel</button>
            <button @click="deleteItem" class="px-4 py-2 text-sm rounded-xl bg-red-700 text-white hover:bg-red-600">Delete</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
