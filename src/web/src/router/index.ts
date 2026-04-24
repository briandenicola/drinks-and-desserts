import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('../views/LoginView.vue'),
    meta: { requiresAuth: false },
  },
  {
    path: '/',
    redirect: '/items',
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: () => import('../views/DashboardView.vue'),
  },
  {
    path: '/capture',
    name: 'Capture',
    component: () => import('../views/CaptureView.vue'),
  },
  {
    path: '/history',
    name: 'History',
    component: () => import('../views/HistoryView.vue'),
  },
  {
    path: '/history/:id',
    name: 'CaptureDetail',
    component: () => import('../views/CaptureDetailView.vue'),
    props: true,
  },
  {
    path: '/items',
    name: 'Items',
    component: () => import('../views/ItemsView.vue'),
  },
  {
    path: '/items/:id',
    name: 'ItemDetail',
    component: () => import('../views/ItemDetailView.vue'),
    props: true,
  },
  {
    path: '/profile',
    name: 'Profile',
    component: () => import('../views/ProfileView.vue'),
  },
  {
    path: '/venues',
    name: 'Venues',
    component: () => import('../views/VenuesView.vue'),
  },
  {
    path: '/venues/:id',
    name: 'VenueDetail',
    component: () => import('../views/VenueDetailView.vue'),
    props: true,
  },
  {
    path: '/search',
    name: 'Search',
    component: () => import('../views/SearchView.vue'),
  },
  {
    path: '/stats',
    name: 'Stats',
    component: () => import('../views/StatsView.vue'),
  },
  {
    path: '/admin',
    name: 'Admin',
    component: () => import('../views/AdminView.vue'),
    meta: { requiresAdmin: true },
  },
  {
    path: '/friends',
    name: 'Friends',
    component: () => import('../views/FriendsView.vue'),
  },
  {
    path: '/friends/join/:code',
    name: 'JoinFriend',
    component: () => import('../views/JoinFriendView.vue'),
    props: true,
  },
  {
    path: '/friends/:friendId',
    name: 'FriendCollection',
    component: () => import('../views/FriendCollectionView.vue'),
    props: true,
  },
  {
    path: '/friends/:friendId/items/:id',
    name: 'FriendItemDetail',
    component: () => import('../views/FriendItemDetailView.vue'),
    props: true,
  },
  {
    path: '/friends/:friendId/venues',
    name: 'FriendVenues',
    component: () => import('../views/FriendVenuesView.vue'),
    props: true,
  },
  {
    path: '/friends/:friendId/venues/:id',
    name: 'FriendVenueDetail',
    component: () => import('../views/FriendVenuesView.vue'),
    props: true,
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  const auth = useAuthStore()

  if (to.meta.requiresAuth !== false && !auth.isAuthenticated) {
    return { name: 'Login' }
  }

  if (to.meta.requiresAdmin && !auth.isAdmin) {
    return { name: 'Items' }
  }
})

export default router
