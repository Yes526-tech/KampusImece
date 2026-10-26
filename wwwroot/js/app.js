/**
 * Kampüsİmece - Canlı Harita, Etkinlik Yönetimi, Öncesi/Sonrası Kanıt Fotoğrafı ve Liderlik Sistemi
 * Selçuk Üniversitesi Teknoloji Fakültesi - Topluma Hizmet Uygulamaları
 */

const KampusImeceApp = {
    // Kampüs Merkez Koordinatları (Alaaddin Keykubat Kampüsü)
    campusCenter: [38.0260, 32.5090],
    defaultZoom: 15,

    // Uygulama Durumu (State)
    events: [],
    filteredEvents: [],
    activeFilter: 'all',
    searchTerm: '',
    map: null,
    markersLayer: null,
    eventMarkers: {}, // eventId -> L.Marker harita eşleştirmesi
    modalMap: null,
    modalMarker: null,
    selectedFile: null,
    photoViews: {}, // eventId -> 'before' | 'after' (Öncesi/Sonrası görünüm tercihi)

    // Başlatma
    init: function () {
        this.initMainMap();
        this.setupFilters();
        this.setupSearch();
        this.setupModal();
        this.setupCompleteModal();
        this.setupReportModal();
        this.setupVolunteerCounter();
        this.setupPhotoUpload();
        this.loadEvents();

        // URL parametresi ile modal açma kontrolü (?openModal=true)
        const urlParams = new URLSearchParams(window.location.search);
        if (urlParams.get('openModal') === 'true') {
            window.openCreateModal();
        }
    },

    // 1. ANA CANLI HARİTA BAŞLATMA
    initMainMap: function () {
        const mapContainer = document.getElementById('campusLiveMap');
        if (!mapContainer) return;

        this.map = L.map('campusLiveMap', {
            zoomControl: true,
            scrollWheelZoom: true
        }).setView(this.campusCenter, this.defaultZoom);

        // Açık kaynak OpenStreetMap katmanı
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap katkıcıları'
        }).addTo(this.map);

        this.markersLayer = L.layerGroup().addTo(this.map);
        this.eventMarkers = {};

        setTimeout(() => {
            if (this.map) this.map.invalidateSize();
        }, 200);

        window.addEventListener('resize', () => {
            if (this.map) this.map.invalidateSize();
        });
    },

    // 2. ETKİNLİKLERİ API'DEN YÜKLEME VE KOORDİNAT DÖNÜŞÜMÜ
    loadEvents: async function () {
        const container = document.getElementById('eventsListContainer');
        const loadingIndicator = document.getElementById('loadingIndicator');

        if (loadingIndicator) loadingIndicator.classList.remove('hidden');

        try {
            const response = await fetch('/api/events');
            if (!response.ok) throw new Error('Etkinlikler sunucudan alınamadı.');

            const rawData = await response.json();
            
            // Tüm ilanların latitude ve longitude değerlerini parseFloat ile hatasız sayısal türe dönüştür
            this.events = rawData.map(ev => ({
                ...ev,
                latitude: parseFloat(ev.latitude),
                longitude: parseFloat(ev.longitude)
            }));

            this.applyFilters();
        } catch (err) {
            console.error('Etkinlik yükleme hatası:', err);
            if (container) {
                container.innerHTML = `
                    <div class="col-span-full py-12 text-center">
                        <div class="inline-flex p-3 rounded-full bg-rose-50 text-rose-500 mb-2">
                            <i data-lucide="alert-octagon" class="w-8 h-8"></i>
                        </div>
                        <h4 class="text-sm font-bold text-slate-800">Veriler Yüklenemedi</h4>
                        <p class="text-xs text-slate-500 mt-1">Sunucu ile iletişim kurulurken bir aksaklık oluştu.</p>
                        <button onclick="KampusImeceApp.loadEvents()" class="mt-4 px-4 py-2 rounded-xl bg-slate-900 text-white text-xs font-semibold hover:bg-slate-800 transition">
                            Tekrar Dene
                        </button>
                    </div>`;
                if (window.lucide) lucide.createIcons();
            }
        } finally {
            if (loadingIndicator) loadingIndicator.classList.add('hidden');
        }
    },

    // 3. FİLTRELEME & ARAMA MANTIĞI
    setupFilters: function () {
        const filterButtons = document.querySelectorAll('.filter-tab-btn');
        const activeClasses = ['bg-white', 'dark:bg-emerald-600', 'text-emerald-800', 'dark:text-white', 'shadow-sm', 'font-semibold', 'border', 'border-slate-200/60', 'dark:border-transparent'];
        const inactiveClasses = ['text-slate-600', 'dark:text-slate-400', 'hover:text-slate-900', 'dark:hover:text-white', 'hover:bg-white/60', 'dark:hover:bg-slate-800', 'font-medium'];

        filterButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                filterButtons.forEach(b => {
                    b.classList.remove(...activeClasses);
                    b.classList.add(...inactiveClasses);
                });

                btn.classList.remove(...inactiveClasses);
                btn.classList.add(...activeClasses);

                this.activeFilter = btn.getAttribute('data-filter') || 'all';
                this.applyFilters();
                if (window.lucide) lucide.createIcons();
            });
        });
    },

    setupSearch: function () {
        const searchInput = document.getElementById('searchEventsInput');
        if (!searchInput) return;

        let debounceTimeout;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(debounceTimeout);
            debounceTimeout = setTimeout(() => {
                this.searchTerm = e.target.value.trim().toLowerCase();
                this.applyFilters();
            }, 250);
        });
    },

    applyFilters: function () {
        this.filteredEvents = this.events.filter(ev => {
            // 1. Kategori Filtresi
            let categoryMatch = true;
            if (this.activeFilter === 'yurt') {
                const combined = (ev.title + ' ' + ev.locationName).toLowerCase();
                categoryMatch = combined.includes('yurt') || combined.includes('kyk');
            } else if (this.activeFilter === 'fakulte') {
                const combined = (ev.title + ' ' + ev.locationName).toLowerCase();
                categoryMatch = combined.includes('fakült') || combined.includes('mühendislik') || combined.includes('teknoloji');
            } else if (this.activeFilter === 'open') {
                categoryMatch = !ev.isQuotaFull && ev.status === 0;
            } else if (this.activeFilter === 'completed') {
                categoryMatch = ev.status === 2; // 2 = EventStatus.Completed
            }

            // 2. Arama Kelimesi Filtresi
            let searchMatch = true;
            if (this.searchTerm) {
                const combined = (ev.title + ' ' + ev.locationName + ' ' + ev.description).toLowerCase();
                searchMatch = combined.includes(this.searchTerm);
            }

            return categoryMatch && searchMatch;
        });

        this.renderMap(this.filteredEvents);
        this.renderCards(this.filteredEvents);
        this.updateStatsCounters(this.filteredEvents);
    },

    // 4. CANLI HARİTA PİNLERİNİ GÜNCELLEME (PARLAYAN YEŞİL ONAY PİNİ DAHİL)
    renderMap: function (events) {
        if (!this.markersLayer) return;
        this.markersLayer.clearLayers();
        this.eventMarkers = {};

        events.forEach(ev => {
            const lat = parseFloat(ev.latitude);
            const lng = parseFloat(ev.longitude);
            if (isNaN(lat) || isNaN(lng)) return;

            const isCompleted = ev.status === 2;
            const isFull = ev.isQuotaFull;
            const remaining = Math.max(0, ev.requiredVolunteers - ev.currentParticipantsCount);

            // Harita Pini Görünümü:
            // 1. Tamamlanan Etkinlikler: Parlayan Zümrüt Yeşili Onay (Checkmark) Pini
            // 2. Kontenjanı Dolanlar: Koyu gri kilitli pin
            // 3. Son 1-2 Kişi Kalanlar: Zıplayan turuncu pin
            // 4. Açık Etkinlikler: Yeşil nabız efektli ağaç pini
            let pinHtml = '';
            if (isCompleted) {
                pinHtml = `
                    <div class="relative group cursor-pointer animate-pulse">
                        <span class="absolute -inset-1 rounded-full bg-emerald-400 opacity-80 animate-ping"></span>
                        <div class="relative w-9 h-9 rounded-full bg-emerald-700 border-2 border-white text-white flex items-center justify-center shadow-xl shadow-emerald-700/60 font-bold text-xs">
                            <i data-lucide="check-check" class="w-5 h-5 stroke-[2.5]"></i>
                        </div>
                    </div>`;
            } else if (isFull) {
                pinHtml = `
                    <div class="relative group cursor-pointer">
                        <div class="w-8 h-8 rounded-full bg-slate-700 border-2 border-white text-white flex items-center justify-center shadow-md">
                            <i data-lucide="lock" class="w-4 h-4"></i>
                        </div>
                    </div>`;
            } else if (remaining <= 2) {
                pinHtml = `
                    <div class="relative group cursor-pointer animate-bounce">
                        <div class="w-9 h-9 rounded-full bg-amber-500 border-2 border-white text-white flex items-center justify-center shadow-lg font-bold text-[11px]">
                            ${remaining}
                        </div>
                    </div>`;
            } else {
                pinHtml = `
                    <div class="relative group cursor-pointer">
                        <span class="absolute -inset-1 rounded-full bg-emerald-400 opacity-60 animate-ping"></span>
                        <div class="relative w-8 h-8 rounded-full bg-emerald-600 border-2 border-white text-white flex items-center justify-center shadow-md">
                            <i data-lucide="trees" class="w-4 h-4"></i>
                        </div>
                    </div>`;
            }

            const customIcon = L.divIcon({
                className: 'custom-event-pin',
                html: pinHtml,
                iconSize: [36, 36],
                iconAnchor: [18, 18],
                popupAnchor: [0, -18]
            });

            const marker = L.marker([lat, lng], { icon: customIcon });

            // Popup İçeriği
            const popupImg = (isCompleted && ev.afterImageUrl) ? ev.afterImageUrl : ev.imageUrl;
            const popupBadge = isCompleted 
                ? `<span class="inline-flex items-center gap-1 text-[10px] font-bold text-emerald-700 bg-emerald-100/90 px-2 py-0.5 rounded-md mb-1"><i data-lucide="sparkles" class="w-3 h-3"></i> Temizlendi & Kanıtlandı</span>`
                : '';

            const popupHtml = `
                <div class="p-1 max-w-[230px] font-sans">
                    <img src="${popupImg}" class="w-full h-24 object-cover rounded-lg mb-2 shadow-sm border border-slate-200" alt="${this.escapeHtml(ev.title)}" />
                    ${popupBadge}
                    <h5 class="text-xs font-bold text-slate-900 leading-tight mb-1">${this.escapeHtml(ev.title)}</h5>
                    <p class="text-[11px] text-emerald-700 font-medium mb-1.5 flex items-center gap-1">
                        <i data-lucide="map-pin" class="w-3 h-3"></i> ${this.escapeHtml(ev.locationName)}
                    </p>
                    <div class="flex items-center justify-between text-[11px] bg-slate-50 p-1.5 rounded-md border border-slate-200 mb-2">
                        <span>Gönüllü:</span>
                        <strong class="${isCompleted ? 'text-emerald-700' : (isFull ? 'text-amber-600' : 'text-emerald-700')}">
                            ${isCompleted ? 'Temizlendi' : `${ev.currentParticipantsCount} / ${ev.requiredVolunteers}`}
                        </strong>
                    </div>
                    <div class="flex items-center gap-1.5">
                        <button onclick="KampusImeceApp.scrollToCard('${ev.id}')" class="flex-1 py-1.5 rounded-md bg-slate-900 hover:bg-slate-800 text-white text-[10px] font-bold transition">
                            Karta Odaklan
                        </button>
                        <button onclick="KampusImeceApp.openReportModal('${ev.id}', '${this.escapeHtml(ev.title)}')" title="Uygunsuz İlanı Bildir" class="p-1.5 rounded-md bg-slate-100 hover:bg-rose-50 hover:text-rose-600 text-slate-500 text-[10px] border border-slate-200 transition">
                            <i data-lucide="flag" class="w-3.5 h-3.5"></i>
                        </button>
                        ${ev.isCreator && !isCompleted ? `
                            <button onclick="KampusImeceApp.deleteEvent('${ev.id}')" title="İlanı Kalıcı Olarak Sil" class="py-1.5 px-2.5 rounded-md bg-rose-50 hover:bg-rose-100 text-rose-600 text-[10px] font-bold border border-rose-200 transition">
                                Sil
                            </button>` : ''}
                    </div>
                </div>
            `;

            marker.bindPopup(popupHtml);
            this.markersLayer.addLayer(marker);
            this.eventMarkers[ev.id] = marker;
        });

        if (window.lucide) lucide.createIcons();
    },

    // 5. ETKİNLİK KARTLARINI RENDER ETME (3'LÜ YAN YANA, FERAH & TAŞMAYAN MİMARİ)
    renderCards: function (events) {
        const container = document.getElementById('eventsListContainer');
        if (!container) return;

        if (events.length === 0) {
            container.innerHTML = `
                <div class="col-span-full py-10 text-center bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 p-6 transition-colors shadow-sm">
                    <div class="w-10 h-10 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-400 mx-auto flex items-center justify-center mb-2">
                        <i data-lucide="inbox" class="w-5 h-5"></i>
                    </div>
                    <h4 class="text-sm font-bold text-slate-800 dark:text-white">İlan Bulunamadı</h4>
                    <p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">Seçilen filtrelere uygun kirlilik noktası bulunmuyor.</p>
                    <button onclick="window.openCreateModal()" class="mt-3 px-3.5 py-1.5 rounded-xl bg-emerald-600 text-white text-xs font-semibold hover:bg-emerald-700 transition shadow-sm">
                        İlk Noktayı Bildir
                    </button>
                </div>`;
            if (window.lucide) lucide.createIcons();
            return;
        }

        const isAuth = document.body.getAttribute('data-authenticated') === 'true';

        let html = '';
        events.forEach(ev => {
            const isCompleted = ev.status === 2;
            const isFull = ev.isQuotaFull;
            const remaining = Math.max(0, ev.requiredVolunteers - ev.currentParticipantsCount);
            const percent = Math.min(100, Math.round((ev.currentParticipantsCount / ev.requiredVolunteers) * 100));

            // Öncesi / Sonrası Görünüm Modu
            const currentPhotoMode = this.photoViews[ev.id] || (isCompleted && ev.afterImageUrl ? 'after' : 'before');
            const displayImageUrl = (currentPhotoMode === 'after' && ev.afterImageUrl) ? ev.afterImageUrl : ev.imageUrl;

            // Durum Rozeti (Frosted Glass Pill)
            let statusBadge = '';
            if (isCompleted) {
                statusBadge = `<span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-[10px] font-bold bg-emerald-700/90 text-white backdrop-blur shadow-sm"><i data-lucide="sparkles" class="w-3 h-3"></i> Temizlendi</span>`;
            } else if (isFull) {
                statusBadge = `<span class="px-2.5 py-1 rounded-lg text-[10px] font-semibold bg-slate-900/85 text-white backdrop-blur shadow-sm">Dolu (${ev.requiredVolunteers}/${ev.requiredVolunteers})</span>`;
            } else if (remaining === 1) {
                statusBadge = `<span class="px-2.5 py-1 rounded-lg text-[10px] font-semibold bg-amber-600/90 text-white backdrop-blur shadow-sm">Son 1 Kişi</span>`;
            } else {
                statusBadge = `<span class="px-2.5 py-1 rounded-lg text-[10px] font-semibold bg-emerald-600/90 text-white backdrop-blur shadow-sm">${remaining} Kişi Aranıyor</span>`;
            }

            // Katılımcı Avatarları
            let avatarsHtml = `<div class="flex items-center -space-x-1.5 overflow-hidden shrink-0">`;
            (ev.participantsList || []).slice(0, 3).forEach(p => {
                avatarsHtml += `
                    <div title="${this.escapeHtml(p.fullName)}" 
                         class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-slate-800 dark:bg-slate-700 text-white text-[9px] font-bold ring-2 ring-white dark:ring-slate-900">
                        ${this.escapeHtml(p.initials)}
                    </div>`;
            });
            if (ev.participantsList && ev.participantsList.length > 3) {
                avatarsHtml += `
                    <div class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200 text-[9px] font-bold ring-2 ring-white dark:ring-slate-900">
                        +${ev.participantsList.length - 3}
                    </div>`;
            }
            avatarsHtml += `</div>`;

            // BUTON EYLEMLERİ (Net, Tekil, Taşmayan ve İç İçe Girmeyen Yapı)
            let actionBtnHtml = '';
            if (isCompleted) {
                actionBtnHtml = `
                    <div class="w-full py-2 px-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-800 dark:text-emerald-300 font-semibold text-xs flex items-center justify-center gap-1.5 border border-emerald-200 dark:border-emerald-800">
                        <i data-lucide="check-circle-2" class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400 shrink-0"></i>
                        <span>Temizlik Tamamlandı (+30 Puan)</span>
                    </div>`;
            } else if (!isAuth) {
                actionBtnHtml = `
                    <a href="/Auth/Login" class="w-full py-2 px-3 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800 font-semibold text-xs flex items-center justify-center gap-1.5 transition">
                        <i data-lucide="log-in" class="w-3.5 h-3.5 shrink-0"></i>
                        <span>Giriş Yap ve Katıl</span>
                    </a>`;
            } else if (ev.isCreator) {
                // İLAN SAHİBİ: Yan yana 2 buton (asla iç içe geçmeyen, taşmayan)
                actionBtnHtml = `
                    <div class="grid grid-cols-2 gap-2">
                        <button type="button" 
                                onclick="KampusImeceApp.openCompleteModal('${ev.id}', '${this.escapeHtml(ev.title)}')" 
                                class="w-full py-2 px-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs flex items-center justify-center gap-1.5 transition shadow-sm active:scale-95 whitespace-nowrap">
                            <i data-lucide="camera" class="w-3.5 h-3.5 shrink-0"></i>
                            <span>Kanıtla</span>
                        </button>
                        <button type="button" 
                                onclick="KampusImeceApp.deleteEvent('${ev.id}')" 
                                class="w-full py-2 px-2.5 rounded-xl border border-rose-200 dark:border-rose-900/60 bg-rose-50/70 dark:bg-rose-950/20 hover:bg-rose-100 dark:hover:bg-rose-900/40 text-rose-600 dark:text-rose-400 font-semibold text-xs flex items-center justify-center gap-1.5 transition active:scale-95 whitespace-nowrap">
                            <i data-lucide="trash-2" class="w-3.5 h-3.5 shrink-0"></i>
                            <span>İlanı Sil</span>
                        </button>
                    </div>`;
            } else if (ev.hasCurrentUserJoined) {
                // KATILMIŞ DİĞER GÖNÜLLÜLER:
                actionBtnHtml = `
                    ${ev.canCurrentUserComplete ? `
                        <div class="grid grid-cols-2 gap-2">
                            <button type="button" 
                                    onclick="KampusImeceApp.toggleJoin('${ev.id}', this)" 
                                    class="w-full py-2 px-2.5 rounded-xl border border-rose-200 dark:border-rose-900/60 bg-rose-50/70 dark:bg-rose-950/20 hover:bg-rose-100 dark:hover:bg-rose-900/40 text-rose-600 dark:text-rose-400 font-semibold text-xs flex items-center justify-center gap-1.5 transition active:scale-95 whitespace-nowrap">
                                <i data-lucide="user-minus" class="w-3.5 h-3.5 shrink-0"></i>
                                <span>Ayrıl</span>
                            </button>
                            <button type="button" 
                                    onclick="KampusImeceApp.openCompleteModal('${ev.id}', '${this.escapeHtml(ev.title)}')" 
                                    class="w-full py-2 px-2.5 rounded-xl bg-amber-500 hover:bg-amber-600 text-white font-semibold text-xs flex items-center justify-center gap-1.5 transition shadow-sm active:scale-95 whitespace-nowrap">
                                <i data-lucide="camera" class="w-3.5 h-3.5 shrink-0"></i>
                                <span>Kanıtla</span>
                            </button>
                        </div>` : `
                        <button type="button" 
                                onclick="KampusImeceApp.toggleJoin('${ev.id}', this)" 
                                class="w-full py-2 px-3 rounded-xl border border-rose-200 dark:border-rose-900/60 bg-rose-50/70 dark:bg-rose-950/20 hover:bg-rose-100 dark:hover:bg-rose-900/40 text-rose-600 dark:text-rose-400 font-semibold text-xs flex items-center justify-center gap-1.5 transition active:scale-95">
                            <i data-lucide="user-minus" class="w-3.5 h-3.5 shrink-0"></i>
                            <span>Katılımı İptal Et</span>
                        </button>`}`;
            } else if (isFull) {
                actionBtnHtml = `
                    <button type="button" 
                            disabled 
                            class="w-full py-2 px-3 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-500 font-medium text-xs flex items-center justify-center gap-1.5 border border-slate-200 dark:border-slate-700 cursor-not-allowed">
                        <i data-lucide="lock" class="w-3.5 h-3.5 shrink-0"></i>
                        <span>Kontenjan Doldu</span>
                    </button>`;
            } else {
                actionBtnHtml = `
                    <button type="button" 
                            onclick="KampusImeceApp.toggleJoin('${ev.id}', this)" 
                            class="w-full py-2 px-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs flex items-center justify-center gap-1.5 transition shadow-sm active:scale-95">
                        <i data-lucide="hand" class="w-3.5 h-3.5 shrink-0"></i>
                        <span>Katılıyorum (+1)</span>
                    </button>`;
            }

            // Tamamlanma Notu
            let completionNoteHtml = '';
            if (isCompleted) {
                completionNoteHtml = `
                    <div class="mb-3 p-2.5 rounded-xl bg-emerald-50/80 dark:bg-emerald-950/30 border border-emerald-200/70 dark:border-emerald-800/50">
                        <div class="flex items-center gap-1.5 text-xs font-semibold text-emerald-800 dark:text-emerald-300 mb-1">
                            <i data-lucide="check-check" class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400 shrink-0"></i>
                            <span>Temizlik Özeti</span>
                        </div>
                        <p class="text-[11px] text-emerald-700 dark:text-emerald-400 italic line-clamp-2">
                            "${this.escapeHtml(ev.completionNote || 'Atıklar toplandı ve geri dönüşüme teslim edildi.')}"
                        </p>
                    </div>`;
            }

            // Öncesi / Sonrası Butonları
            let beforeAfterToggleHtml = '';
            if (isCompleted && ev.afterImageUrl) {
                beforeAfterToggleHtml = `
                    <div class="absolute bottom-2.5 right-2.5 flex items-center bg-black/60 backdrop-blur-md p-0.5 rounded-lg text-[10px] font-medium z-10 border border-white/10 shadow-sm">
                        <button type="button" 
                                onclick="event.stopPropagation(); KampusImeceApp.showPhoto('${ev.id}', 'before')" 
                                id="btn-photo-before-${ev.id}" 
                                class="px-2 py-0.5 rounded-md transition ${currentPhotoMode === 'before' ? 'bg-emerald-600 text-white font-semibold shadow' : 'text-slate-300 hover:text-white'}">
                            Öncesi
                        </button>
                        <button type="button" 
                                onclick="event.stopPropagation(); KampusImeceApp.showPhoto('${ev.id}', 'after')" 
                                id="btn-photo-after-${ev.id}" 
                                class="px-2 py-0.5 rounded-md transition ${currentPhotoMode === 'after' ? 'bg-emerald-600 text-white font-semibold shadow' : 'text-slate-300 hover:text-white'}">
                            Sonrası ✨
                        </button>
                    </div>`;
            }

            // Tarih Biçimlendirme
            const dateObj = new Date(ev.scheduledAt);
            const formattedDate = dateObj.toLocaleDateString('tr-TR', { weekday: 'short', hour: '2-digit', minute: '2-digit' });

            html += `
                <div id="event-card-${ev.id}" class="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/90 dark:border-slate-800 shadow-sm hover:shadow-md hover:border-emerald-500/30 dark:hover:border-emerald-500/30 transition-all duration-200 flex flex-col overflow-hidden group">
                    <!-- ÜST RESİM VE ROZETLER -->
                    <div class="relative h-36 w-full overflow-hidden bg-slate-100 dark:bg-slate-800 shrink-0">
                        <img id="event-img-${ev.id}" src="${displayImageUrl}" class="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500" alt="${this.escapeHtml(ev.title)}" />
                        <div class="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-black/20 pointer-events-none"></div>

                        <!-- Sol Üst: Konum (Taşmayan, Temiz Etiket) -->
                        <div class="absolute top-2.5 left-2.5 z-10">
                            <span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[11px] font-medium bg-black/60 backdrop-blur-md text-white shadow-sm max-w-[170px]">
                                <i data-lucide="map-pin" class="w-3 h-3 text-emerald-400 shrink-0"></i>
                                <span class="truncate">${this.escapeHtml(ev.locationName)}</span>
                            </span>
                        </div>

                        <!-- Sağ Üst: Durum Rozeti ve Şikayet Bildirimi Butonu -->
                        <div class="absolute top-2.5 right-2.5 flex items-center gap-1.5 z-10">
                            ${statusBadge}
                            <button type="button" 
                                    onclick="event.stopPropagation(); KampusImeceApp.openReportModal('${ev.id}', '${this.escapeHtml(ev.title)}')" 
                                    title="Moderatöre Bildir" 
                                    class="p-1.5 rounded-lg bg-black/50 hover:bg-rose-600 text-white/90 hover:text-white backdrop-blur-md transition-colors active:scale-95 shadow-sm">
                                <i data-lucide="flag" class="w-3 h-3"></i>
                            </button>
                        </div>

                        <!-- Sol Alt: Tarih -->
                        <div class="absolute bottom-2.5 left-2.5 text-white text-[11px] font-medium flex items-center gap-1 drop-shadow-md">
                            <i data-lucide="calendar" class="w-3 h-3 text-emerald-300 shrink-0"></i>
                            <span>${formattedDate}</span>
                        </div>

                        <!-- Sağ Alt: Öncesi/Sonrası Butonu -->
                        ${beforeAfterToggleHtml}
                    </div>

                    <!-- KART GÖVDESİ (HER BİLGİ NET, FERAH & KESİNTİSİZ) -->
                    <div class="p-3.5 flex-1 flex flex-col justify-between">
                        <div>
                            <!-- Başlık (2 satıra kadar net okunaklı, boy hizalı) -->
                            <h4 class="text-xs font-bold text-slate-900 dark:text-white leading-snug line-clamp-2 min-h-[2rem] group-hover:text-emerald-600 dark:group-hover:text-emerald-400 transition-colors" title="${this.escapeHtml(ev.title)}">
                                ${this.escapeHtml(ev.title)}
                            </h4>
                            <!-- Açıklama (Her detay rahatça okunur) -->
                            <p class="text-[11px] text-slate-600 dark:text-slate-400 line-clamp-2 leading-relaxed mt-1 mb-2.5 min-h-[1.8rem]">
                                ${this.escapeHtml(ev.description || 'Açıklama belirtilmedi.')}
                            </p>
                        </div>

                        <div>
                            <!-- İLERLEME ÇUBUĞU (Ferah & Net) -->
                            ${!isCompleted ? `
                                <div class="mb-2.5 p-2 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-100 dark:border-slate-800">
                                    <div class="flex items-center justify-between text-[11px] mb-1">
                                        <span class="flex items-center gap-1 text-slate-500 dark:text-slate-400">
                                            <i data-lucide="users" class="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400"></i>
                                            <span>Gönüllü Katılımı</span>
                                        </span>
                                        <span class="font-bold ${isFull ? 'text-amber-500' : 'text-slate-700 dark:text-slate-200'}">
                                            ${ev.currentParticipantsCount} / ${ev.requiredVolunteers}
                                        </span>
                                    </div>
                                    <div class="w-full h-1.5 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
                                        <div class="h-full rounded-full transition-all duration-300 ${isFull ? 'bg-amber-500' : 'bg-emerald-500'}" style="width: ${percent}%"></div>
                                    </div>
                                </div>` : ''}

                            <!-- Tamamlanma Rapor Notu -->
                            ${completionNoteHtml}

                            <!-- Katılımcılar & İlan Sahibi (Taşmayan, Net Çizgi) -->
                            <div class="flex items-center justify-between mb-2.5 text-[11px] pt-1 border-t border-slate-100 dark:border-slate-800/80">
                                <div class="flex items-center gap-1.5 min-w-0">
                                    ${avatarsHtml}
                                    <span class="text-slate-500 dark:text-slate-400 text-[10px] truncate">${ev.currentParticipantsCount} kişi</span>
                                </div>
                                <span class="text-slate-500 dark:text-slate-400 text-[10px] shrink-0">
                                    İlan: <strong class="text-slate-700 dark:text-slate-200 font-semibold">${this.escapeHtml((ev.creatorName || '').split(' ')[0])}</strong>
                                </span>
                            </div>

                            <!-- Butonlar (Geniş, Rahat Tıklanır, Asla İç İçe Geçmeyen) -->
                            <div>
                                ${actionBtnHtml}
                            </div>
                        </div>
                    </div>
                </div>
            `;
        });

        container.innerHTML = html;
        if (window.lucide) lucide.createIcons();
    },

    // 6. ÖNCESİ (BEFORE) / SONRASI (AFTER) FOTOĞRAF DEĞİŞTİRME YARDIMCISI
    showPhoto: function (eventId, mode) {
        const ev = this.events.find(e => e.id === eventId);
        if (!ev) return;

        const imgEl = document.getElementById(`event-img-${eventId}`);
        const btnBefore = document.getElementById(`btn-photo-before-${eventId}`);
        const btnAfter = document.getElementById(`btn-photo-after-${eventId}`);

        this.photoViews[eventId] = mode;

        if (imgEl) {
            imgEl.src = (mode === 'after' && ev.afterImageUrl) ? ev.afterImageUrl : ev.imageUrl;
        }

        if (btnBefore && btnAfter) {
            if (mode === 'before') {
                btnBefore.classList.add('bg-emerald-600', 'text-white', 'shadow');
                btnBefore.classList.remove('text-slate-300');
                btnAfter.classList.remove('bg-emerald-600', 'text-white', 'shadow');
                btnAfter.classList.add('text-slate-300');
            } else {
                btnAfter.classList.add('bg-emerald-600', 'text-white', 'shadow');
                btnAfter.classList.remove('text-slate-300');
                btnBefore.classList.remove('bg-emerald-600', 'text-white', 'shadow');
                btnBefore.classList.add('text-slate-300');
            }
        }
    },

    // 7. TOGGLE JOIN (KATIL / AYRIL) AJAX İŞLEMİ
    toggleJoin: async function (eventId, btnElement) {
        if (!eventId) return;

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        const originalText = btnElement ? btnElement.innerHTML : '';
        if (btnElement) {
            btnElement.disabled = true;
            btnElement.innerHTML = `<span class="inline-flex items-center gap-1.5"><i data-lucide="loader-2" class="w-4 h-4 animate-spin"></i> İşleniyor...</span>`;
            if (window.lucide) lucide.createIcons();
        }

        try {
            const formData = new FormData();
            if (token) formData.append('__RequestVerificationToken', token);

            const response = await fetch(`/api/events/${eventId}/toggle-join`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            const result = await response.json();

            if (!response.ok) {
                if (window.showToast) window.showToast(result.message || 'Katılım işlemi başarısız oldu.', 'error');
            } else {
                if (window.showToast) window.showToast(result.message, 'success');
                await this.loadEvents();
            }
        } catch (err) {
            console.error('Katılım hatası:', err);
            if (window.showToast) window.showToast('Sunucu ile bağlantı kurulamadı.', 'error');
        } finally {
            if (btnElement) {
                btnElement.disabled = false;
                btnElement.innerHTML = originalText;
                if (window.lucide) lucide.createIcons();
            }
        }
    },

    // 8. GÜVENLİ İLAN SİLME (DELETE /api/events/{id})
    deleteEvent: async function (eventId) {
        if (!eventId) return;

        const confirmed = confirm('Bu kirlilik ilanını silmek istediğinize emin misiniz?\n\nİlana ait fotoğraf ve katılımcı kayıtları kalıcı olarak temizlenecektir.');
        if (!confirmed) return;

        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        try {
            const response = await fetch(`/api/events/${eventId}`, {
                method: 'DELETE',
                headers: {
                    'RequestVerificationToken': token
                }
            });

            const result = await response.json();

            if (!response.ok) {
                if (window.showToast) {
                    window.showToast(result.message || 'İlan silinemedi.', 'error');
                } else {
                    alert(result.message || 'İlan silinemedi.');
                }
                return;
            }

            // 1. Ana haritadaki ilgili marker'ı anında sil
            if (this.eventMarkers[eventId]) {
                this.markersLayer.removeLayer(this.eventMarkers[eventId]);
                delete this.eventMarkers[eventId];
            }

            // 2. Kartı DOM'dan yumuşak bir animasyonla kaldır
            const cardEl = document.getElementById(`event-card-${eventId}`);
            if (cardEl) {
                cardEl.style.transition = 'all 0.35s ease-out';
                cardEl.style.opacity = '0';
                cardEl.style.transform = 'scale(0.95)';
                setTimeout(() => {
                    if (cardEl && cardEl.parentNode) {
                        cardEl.remove();
                    }
                }, 350);
            }

            // 3. Olay dizilerini güncelle
            this.events = this.events.filter(e => e.id !== eventId);
            this.filteredEvents = this.filteredEvents.filter(e => e.id !== eventId);
            this.updateStatsCounters(this.events);

            if (window.showToast) {
                window.showToast(result.message || 'İlan başarıyla silindi.', 'success');
            }

            if (this.filteredEvents.length === 0) {
                this.renderCards(this.filteredEvents);
            }
        } catch (err) {
            console.error('İlan silme hatası:', err);
            if (window.showToast) {
                window.showToast('Sunucu ile bağlantı kurulamadı.', 'error');
            }
        }
    },

    // 9. TEMİZLİK KANITI YÜKLEME VE TAMAMLAMA MODALI
    setupCompleteModal: function () {
        const modal = document.getElementById('completeEventModal');
        const form = document.getElementById('completeEventForm');
        const fileInput = document.getElementById('afterPhotoFileInput');
        const dropZone = document.getElementById('afterPhotoDropZone');
        const previewImg = document.getElementById('afterPhotoPreviewImage');
        const previewContainer = document.getElementById('afterPhotoPreviewContainer');
        const promptEl = document.getElementById('afterPhotoDropPrompt');
        const removeBtn = document.getElementById('btnRemoveAfterPhoto');

        if (!modal || !form) return;

        // Global Modal Açma
        this.openCompleteModal = (eventId, title) => {
            const idInput = document.getElementById('completeEventId');
            const titleEl = document.getElementById('completeEventTitle');
            const alertBox = document.getElementById('completeModalAlert');

            if (idInput) idInput.value = eventId;
            if (titleEl) titleEl.innerText = title;
            if (alertBox) alertBox.classList.add('hidden');

            form.reset();
            this.resetAfterPhotoPreview();

            modal.classList.remove('hidden');
            modal.classList.add('flex');
            document.body.classList.add('overflow-hidden');
            if (window.lucide) lucide.createIcons();
        };

        // Global Modal Kapatma
        window.closeCompleteModal = () => {
            modal.classList.add('hidden');
            modal.classList.remove('flex');
            document.body.classList.remove('overflow-hidden');
            form.reset();
            this.resetAfterPhotoPreview();
        };

        // Dosya Seçim & Sürükle Bırak
        if (dropZone && fileInput) {
            ['dragenter', 'dragover'].forEach(event => {
                dropZone.addEventListener(event, (e) => {
                    e.preventDefault();
                    dropZone.classList.add('border-emerald-500', 'bg-emerald-50/70');
                });
            });

            ['dragleave', 'drop'].forEach(event => {
                dropZone.addEventListener(event, (e) => {
                    e.preventDefault();
                    dropZone.classList.remove('border-emerald-500', 'bg-emerald-50/70');
                });
            });

            dropZone.addEventListener('drop', (e) => {
                const files = e.dataTransfer.files;
                if (files && files.length > 0) {
                    fileInput.files = files;
                    this.handleAfterFileSelected(files[0]);
                }
            });

            fileInput.addEventListener('change', (e) => {
                if (e.target.files && e.target.files.length > 0) {
                    this.handleAfterFileSelected(e.target.files[0]);
                }
            });
        }

        if (removeBtn) {
            removeBtn.addEventListener('click', () => {
                this.resetAfterPhotoPreview();
            });
        }

        // Form Gönderme AJAX
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const eventId = document.getElementById('completeEventId')?.value;
            const submitBtn = document.getElementById('btnSubmitCompleteEvent');
            const alertBox = document.getElementById('completeModalAlert');

            if (!eventId) return;

            if (alertBox) alertBox.classList.add('hidden');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = `<i data-lucide="loader-2" class="w-4 h-4 animate-spin"></i> Kanıt Doğrulanıyor...`;
                if (window.lucide) lucide.createIcons();
            }

            try {
                const formData = new FormData(form);
                const response = await fetch(`/api/events/${eventId}/complete`, {
                    method: 'POST',
                    body: formData
                });

                if (response.redirected && response.url.includes('/Auth/Login')) {
                    window.location.href = '/Auth/Login';
                    return;
                }

                let result = {};
                try {
                    result = await response.json();
                } catch {
                    result = { 
                        success: false, 
                        message: response.status === 413 
                            ? 'Kanıt fotoğrafı boyutu çok büyük (Maks 15 MB).' 
                            : 'Sunucu yanıt veremedi veya oturumunuz sonlandı.' 
                    };
                }

                if (!response.ok) {
                    const errorMsg = result.message || 'Temizlik tamamlanırken bir hata oluştu.';
                    if (alertBox) {
                        alertBox.innerHTML = errorMsg;
                        alertBox.classList.remove('hidden');
                        alertBox.classList.add('bg-rose-50', 'text-rose-700', 'border-rose-200');
                    }
                    if (window.showToast) {
                        window.showToast(errorMsg, 'error');
                    }
                } else {
                    // Başarılı: Modalı kapat
                    window.closeCompleteModal();

                    // Güncellenmiş nesneyi listeye yansıt
                    const updatedEvent = result.data;
                    if (updatedEvent) {
                        updatedEvent.latitude = parseFloat(updatedEvent.latitude);
                        updatedEvent.longitude = parseFloat(updatedEvent.longitude);

                        const idx = this.events.findIndex(ev => ev.id === eventId);
                        if (idx !== -1) {
                            this.events[idx] = updatedEvent;
                        }
                    }
                    this.photoViews[eventId] = 'after';

                    // Haritayı ve kartları güncelle
                    this.applyFilters();

                    if (window.showToast) {
                        window.showToast(result.message || 'Tebrikler! Temizlik kanıtlandı, tüm katılımcılar +30 Bonus Puan kazandı!', 'success');
                    }

                    // Karta odaklan
                    setTimeout(() => {
                        this.scrollToCard(eventId);
                        if (this.eventMarkers[eventId]) {
                            this.eventMarkers[eventId].openPopup();
                        }
                    }, 400);
                }
            } catch (err) {
                console.error('Tamamlama hatası:', err);
                if (alertBox) {
                    alertBox.innerText = 'Sunucuya bağlanırken bir hata oluştu.';
                    alertBox.classList.remove('hidden');
                }
            } finally {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = `<i data-lucide="check" class="w-3.5 h-3.5"></i> Temizliği Onayla`;
                    if (window.lucide) lucide.createIcons();
                }
            }
        });
    },

    handleAfterFileSelected: function (file) {
        if (!file) return;
        const previewImg = document.getElementById('afterPhotoPreviewImage');
        const previewContainer = document.getElementById('afterPhotoPreviewContainer');
        const dropPrompt = document.getElementById('afterPhotoDropPrompt');
        const fileInput = document.getElementById('afterPhotoFileInput');

        const isImage = file.type.startsWith('image/') || /\.(jpe?g|png|webp|heic|bmp)$/i.test(file.name || '');
        if (!isImage) {
            if (window.showToast) window.showToast('Lütfen geçerli bir resim dosyası seçiniz (.jpg, .png veya .webp).', 'error');
            else alert('Lütfen geçerli bir resim dosyası seçiniz.');
            this.resetAfterPhotoPreview();
            return;
        }

        if (file.size > 15 * 1024 * 1024) {
            if (window.showToast) window.showToast('Fotoğraf boyutu 15 MB sınırını aşıyor.', 'error');
            else alert('Fotoğraf boyutu 15 MB sınırını aşıyor.');
            this.resetAfterPhotoPreview();
            return;
        }

        try {
            if (previewImg) {
                if (previewImg._blobUrl) {
                    URL.revokeObjectURL(previewImg._blobUrl);
                }
                const blobUrl = URL.createObjectURL(file);
                previewImg._blobUrl = blobUrl;
                previewImg.src = blobUrl;
            }
            if (previewContainer) previewContainer.classList.remove('hidden');
            if (dropPrompt) dropPrompt.classList.add('hidden');
            if (fileInput) fileInput.classList.add('pointer-events-none');
        } catch (err) {
            console.error('Önizleme hatası:', err);
        }
    },

    resetAfterPhotoPreview: function () {
        const fileInput = document.getElementById('afterPhotoFileInput');
        const previewImg = document.getElementById('afterPhotoPreviewImage');
        const previewContainer = document.getElementById('afterPhotoPreviewContainer');
        const dropPrompt = document.getElementById('afterPhotoDropPrompt');

        if (fileInput) {
            fileInput.value = '';
            fileInput.classList.remove('pointer-events-none');
        }
        if (previewImg) {
            if (previewImg._blobUrl) {
                URL.revokeObjectURL(previewImg._blobUrl);
                previewImg._blobUrl = null;
            }
            previewImg.src = '';
        }
        if (previewContainer) previewContainer.classList.add('hidden');
        if (dropPrompt) dropPrompt.classList.remove('hidden');
    },

    // 9.5. ŞİKAYET / RAPORLAMA MODALI VE GÖNDERİMİ
    openReportModal: function (eventId, eventTitle) {
        const isAuth = document.body.getAttribute('data-authenticated') === 'true';
        if (!isAuth) {
            if (window.showToast) window.showToast('İlanı şikayet edebilmek için lütfen giriş yapınız.', 'info');
            setTimeout(() => { window.location.href = '/Auth/Login?returnUrl=/'; }, 350);
            return;
        }

        const modal = document.getElementById('reportEventModal');
        const eventIdInput = document.getElementById('reportEventId');
        const titleSpan = document.getElementById('reportEventTitle');
        const alertBox = document.getElementById('reportModalAlert');

        if (alertBox) alertBox.classList.add('hidden');
        if (eventIdInput) eventIdInput.value = eventId;
        if (titleSpan) titleSpan.innerText = eventTitle;

        if (modal) {
            modal.classList.remove('hidden');
            modal.classList.add('flex');
            document.body.classList.add('overflow-hidden');
        }
    },

    closeReportModal: function () {
        const modal = document.getElementById('reportEventModal');
        const form = document.getElementById('reportEventForm');
        if (modal) {
            modal.classList.add('hidden');
            modal.classList.remove('flex');
            document.body.classList.remove('overflow-hidden');
        }
        if (form) form.reset();
    },

    setupReportModal: function () {
        const form = document.getElementById('reportEventForm');
        if (!form) return;

        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const submitBtn = document.getElementById('btnSubmitReportEvent');
            const alertBox = document.getElementById('reportModalAlert');

            if (alertBox) alertBox.classList.add('hidden');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = `<i data-lucide="loader-2" class="w-3.5 h-3.5 animate-spin"></i> İletiliyor...`;
                if (window.lucide) lucide.createIcons();
            }

            try {
                const formData = new FormData(form);
                const response = await fetch('/api/reports', {
                    method: 'POST',
                    body: formData
                });

                if (response.redirected && response.url.includes('/Auth/Login')) {
                    window.location.href = '/Auth/Login';
                    return;
                }

                let result = {};
                try {
                    result = await response.json();
                } catch {
                    result = { success: false, message: 'Sunucu yanıtı alınamadı.' };
                }

                if (!response.ok) {
                    const errorMsg = result.message || (result.errors ? result.errors.join('<br>') : 'Şikayet iletilemedi.');
                    if (alertBox) {
                        alertBox.innerHTML = errorMsg;
                        alertBox.className = 'p-2.5 rounded-xl text-xs font-medium border bg-rose-50 text-rose-700 border-rose-200';
                        alertBox.classList.remove('hidden');
                    }
                    if (window.showToast) window.showToast(errorMsg.replace(/<[^>]*>?/gm, ' '), 'error');
                } else {
                    this.closeReportModal();
                    if (window.showToast) {
                        window.showToast(result.message || 'Şikayetiniz moderatör ekibine iletildi. Teşekkür ederiz.', 'success');
                    }
                }
            } catch (err) {
                console.error('Şikayet gönderme hatası:', err);
                if (alertBox) {
                    alertBox.innerText = 'Sunucuya bağlanırken bir hata oluştu.';
                    alertBox.className = 'p-2.5 rounded-xl text-xs font-medium border bg-rose-50 text-rose-700 border-rose-200';
                    alertBox.classList.remove('hidden');
                }
            } finally {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = `<i data-lucide="send" class="w-3.5 h-3.5"></i> Şikayeti İlet`;
                    if (window.lucide) lucide.createIcons();
                }
            }
        });
    },

    // 10. MODAL YÖNETİMİ & MİNİ HARİTADAN KONUM SEÇİCİ
    setupModal: function () {
        const modal = document.getElementById('createEventModal');
        const form = document.getElementById('createEventForm');
        if (!modal || !form) return;

        // Global Modal Açma Fonksiyonu
        window.openCreateModal = () => {
            const isAuth = document.body.getAttribute('data-authenticated') === 'true';
            if (!isAuth) {
                if (window.showToast) window.showToast('Kirlilik bildirimi yapabilmek için lütfen giriş yapınız.', 'info');
                setTimeout(() => { window.location.href = '/Auth/Login?returnUrl=/'; }, 300);
                return;
            }

            modal.classList.remove('hidden');
            modal.classList.add('flex');
            document.body.classList.add('overflow-hidden');

            this.initModalPickerMap();

            setTimeout(() => {
                if (this.modalMap) {
                    this.modalMap.invalidateSize();
                }
            }, 150);

            setTimeout(() => {
                if (this.modalMap) {
                    this.modalMap.invalidateSize();
                }
            }, 350);

            const dateInput = form.querySelector('input[name="ScheduledAt"]');
            if (dateInput && !dateInput.value) {
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);
                tomorrow.setHours(14, 0, 0, 0);
                tomorrow.setMinutes(tomorrow.getMinutes() - tomorrow.getTimezoneOffset());
                dateInput.value = tomorrow.toISOString().slice(0, 16);
            }
        };

        window.closeCreateModal = () => {
            modal.classList.add('hidden');
            modal.classList.remove('flex');
            document.body.classList.remove('overflow-hidden');
            form.reset();
            this.resetPhotoPreview();
        };

        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const submitBtn = document.getElementById('btnSubmitCreateEvent');
            const alertBox = document.getElementById('modalFormAlert');

            if (alertBox) alertBox.classList.add('hidden');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = `<i data-lucide="loader-2" class="w-3.5 h-3.5 animate-spin"></i> Yayınlanıyor...`;
                if (window.lucide) lucide.createIcons();
            }

            try {
                const formData = new FormData(form);
                const response = await fetch('/api/events', {
                    method: 'POST',
                    body: formData
                });

                if (response.redirected && response.url.includes('/Auth/Login')) {
                    window.location.href = '/Auth/Login';
                    return;
                }

                let result = {};
                try {
                    result = await response.json();
                } catch {
                    result = { 
                        success: false, 
                        message: response.status === 413 
                            ? 'Fotoğraf boyutu çok büyük (Maks 15 MB).' 
                            : 'Sunucu yanıt veremedi veya oturumunuz sonlandı.' 
                    };
                }

                if (!response.ok) {
                    const errorMsg = result.message || (result.errors ? result.errors.join('<br>') : 'Hata oluştu.');
                    if (alertBox) {
                        alertBox.innerHTML = errorMsg;
                        alertBox.classList.remove('hidden', 'bg-emerald-50', 'text-emerald-700', 'border-emerald-200');
                        alertBox.classList.add('bg-rose-50', 'text-rose-700', 'border-rose-200');
                        alertBox.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                    }
                    if (window.showToast) {
                        window.showToast(errorMsg.replace(/<[^>]*>?/gm, ' '), 'error');
                    }
                } else {
                    const newEventData = result.data || result;
                    newEventData.latitude = parseFloat(newEventData.latitude);
                    newEventData.longitude = parseFloat(newEventData.longitude);

                    window.closeCreateModal();

                    this.events.unshift(newEventData);
                    this.applyFilters();

                    if (this.map && !isNaN(newEventData.latitude) && !isNaN(newEventData.longitude)) {
                        this.map.flyTo([newEventData.latitude, newEventData.longitude], 16, {
                            animate: true,
                            duration: 1.2
                        });
                    }

                    setTimeout(() => {
                        this.scrollToCard(newEventData.id);
                        if (this.eventMarkers[newEventData.id]) {
                            this.eventMarkers[newEventData.id].openPopup();
                        }
                    }, 500);

                    if (window.showToast) {
                        window.showToast(result.message || 'Kirlilik noktası bildirildi ve ilk gönüllü oldunuz!', 'success');
                    }
                }
            } catch (err) {
                console.error('İlan oluşturma hatası:', err);
                if (alertBox) {
                    alertBox.innerText = 'Sunucuya bağlanırken bir hata oluştu.';
                    alertBox.classList.remove('hidden');
                }
            } finally {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = `<i data-lucide="check" class="w-3.5 h-3.5"></i> İlanı Başlat`;
                    if (window.lucide) lucide.createIcons();
                }
            }
        });

        const btnGps = document.getElementById('btnGetGpsLocation');
        if (btnGps) {
            btnGps.addEventListener('click', () => {
                if (!navigator.geolocation) {
                    alert('Tarayıcınız konum servisini desteklemiyor.');
                    return;
                }

                btnGps.innerHTML = `<i data-lucide="loader-2" class="w-3.5 h-3.5 animate-spin"></i> Konum Alınıyor...`;
                if (window.lucide) lucide.createIcons();

                navigator.geolocation.getCurrentPosition(
                    (pos) => {
                        const lat = pos.coords.latitude;
                        const lng = pos.coords.longitude;
                        this.setModalLocation(lat, lng);
                        btnGps.innerHTML = `<i data-lucide="check" class="w-3.5 h-3.5 text-emerald-600"></i> Konum Alındı!`;
                        if (window.lucide) lucide.createIcons();
                        setTimeout(() => {
                            btnGps.innerHTML = `<i data-lucide="navigation" class="w-3.5 h-3.5"></i> Mevcut GPS Konumumu Al`;
                            if (window.lucide) lucide.createIcons();
                        }, 2000);
                    },
                    (err) => {
                        console.warn('GPS Hatası:', err);
                        alert('Konum alınamadı. Lütfen tarayıcı izinlerinizi kontrol edin veya haritaya tıklayarak seçin.');
                        btnGps.innerHTML = `<i data-lucide="navigation" class="w-3.5 h-3.5"></i> Mevcut GPS Konumumu Al`;
                        if (window.lucide) lucide.createIcons();
                    },
                    { enableHighAccuracy: true, timeout: 8000 }
                );
            });
        }
    },

    // 11. MODAL İÇİNDEKİ MİNİ HARİTA (SEÇİM HARİTASI)
    initModalPickerMap: function () {
        const pickerContainer = document.getElementById('modalPickerMap');
        if (!pickerContainer) return;

        const inputLat = document.getElementById('inputLatitude');
        const inputLng = document.getElementById('inputLongitude');
        const initialLat = inputLat ? parseFloat(inputLat.value) : this.campusCenter[0];
        const initialLng = inputLng ? parseFloat(inputLng.value) : this.campusCenter[1];

        if (!this.modalMap) {
            this.modalMap = L.map('modalPickerMap', {
                zoomControl: true
            }).setView([initialLat, initialLng], 16);

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '© OpenStreetMap'
            }).addTo(this.modalMap);

            const pinIcon = L.divIcon({
                className: 'modal-pin',
                html: `<div class="w-8 h-8 rounded-full bg-rose-600 border-2 border-white shadow-xl text-white flex items-center justify-center animate-bounce"><i data-lucide="map-pin" class="w-4 h-4"></i></div>`,
                iconSize: [32, 32],
                iconAnchor: [16, 32]
            });

            this.modalMarker = L.marker([initialLat, initialLng], {
                icon: pinIcon,
                draggable: true
            }).addTo(this.modalMap);

            this.modalMarker.on('dragend', (e) => {
                const pos = e.target.getLatLng();
                this.updateCoordinateInputs(pos.lat, pos.lng);
            });

            this.modalMap.on('click', (e) => {
                this.setModalLocation(e.latlng.lat, e.latlng.lng);
            });
        } else {
            this.modalMap.invalidateSize();
            this.setModalLocation(initialLat, initialLng);
        }

        if (window.lucide) lucide.createIcons();
    },

    setModalLocation: function (lat, lng) {
        if (this.modalMarker) {
            this.modalMarker.setLatLng([lat, lng]);
        }
        if (this.modalMap) {
            this.modalMap.panTo([lat, lng]);
        }
        this.updateCoordinateInputs(lat, lng);
    },

    updateCoordinateInputs: function (lat, lng) {
        const inputLat = document.getElementById('inputLatitude');
        const inputLng = document.getElementById('inputLongitude');
        const badgeLat = document.getElementById('displayLatitude');
        const badgeLng = document.getElementById('displayLongitude');

        const latFixed = Number(lat).toFixed(6);
        const lngFixed = Number(lng).toFixed(6);

        if (inputLat) inputLat.value = latFixed;
        if (inputLng) inputLng.value = lngFixed;
        if (badgeLat) badgeLat.innerText = latFixed;
        if (badgeLng) badgeLng.innerText = lngFixed;
    },

    // 12. FOTOĞRAF SÜRÜKLE-BIRAK & ÖNİZLEME
    setupPhotoUpload: function () {
        const dropZone = document.getElementById('photoDropZone');
        const fileInput = document.getElementById('photoFileInput');
        const previewImg = document.getElementById('photoPreviewImage');
        const previewContainer = document.getElementById('photoPreviewContainer');
        const removeBtn = document.getElementById('btnRemovePhoto');

        if (!dropZone || !fileInput) return;

        ['dragenter', 'dragover'].forEach(event => {
            dropZone.addEventListener(event, (e) => {
                e.preventDefault();
                dropZone.classList.add('border-emerald-500', 'bg-emerald-50/70');
            });
        });

        ['dragleave', 'drop'].forEach(event => {
            dropZone.addEventListener(event, (e) => {
                e.preventDefault();
                dropZone.classList.remove('border-emerald-500', 'bg-emerald-50/70');
            });
        });

        dropZone.addEventListener('drop', (e) => {
            const files = e.dataTransfer.files;
            if (files && files.length > 0) {
                fileInput.files = files;
                this.handleFileSelected(files[0]);
            }
        });

        fileInput.addEventListener('change', (e) => {
            if (e.target.files && e.target.files.length > 0) {
                this.handleFileSelected(e.target.files[0]);
            }
        });

        if (removeBtn) {
            removeBtn.addEventListener('click', () => {
                this.resetPhotoPreview();
            });
        }
    },

    handleFileSelected: function (file) {
        if (!file) return;
        const previewImg = document.getElementById('photoPreviewImage');
        const previewContainer = document.getElementById('photoPreviewContainer');
        const dropPrompt = document.getElementById('photoDropPrompt');
        const fileInput = document.getElementById('photoFileInput');

        const isImage = file.type.startsWith('image/') || /\.(jpe?g|png|webp|heic|bmp)$/i.test(file.name || '');
        if (!isImage) {
            if (window.showToast) window.showToast('Lütfen geçerli bir resim dosyası seçiniz (.jpg, .png veya .webp).', 'error');
            else alert('Lütfen geçerli bir resim dosyası seçiniz.');
            this.resetPhotoPreview();
            return;
        }

        if (file.size > 15 * 1024 * 1024) {
            if (window.showToast) window.showToast('Fotoğraf boyutu 15 MB sınırını aşıyor.', 'error');
            else alert('Fotoğraf boyutu 15 MB sınırını aşıyor.');
            this.resetPhotoPreview();
            return;
        }

        try {
            if (previewImg) {
                if (previewImg._blobUrl) {
                    URL.revokeObjectURL(previewImg._blobUrl);
                }
                const blobUrl = URL.createObjectURL(file);
                previewImg._blobUrl = blobUrl;
                previewImg.src = blobUrl;
            }
            if (previewContainer) previewContainer.classList.remove('hidden');
            if (dropPrompt) dropPrompt.classList.add('hidden');
            if (fileInput) fileInput.classList.add('pointer-events-none');
        } catch (err) {
            console.error('Önizleme hatası:', err);
        }
    },

    resetPhotoPreview: function () {
        const fileInput = document.getElementById('photoFileInput');
        const previewImg = document.getElementById('photoPreviewImage');
        const previewContainer = document.getElementById('photoPreviewContainer');
        const dropPrompt = document.getElementById('photoDropPrompt');

        if (fileInput) {
            fileInput.value = '';
            fileInput.classList.remove('pointer-events-none');
        }
        if (previewImg) {
            if (previewImg._blobUrl) {
                URL.revokeObjectURL(previewImg._blobUrl);
                previewImg._blobUrl = null;
            }
            previewImg.src = '';
        }
        if (previewContainer) previewContainer.classList.add('hidden');
        if (dropPrompt) dropPrompt.classList.remove('hidden');
    },

    // 13. GEREKLİ KİŞİ SAYISI (+ / -) SAYAÇ BİLEŞENİ
    setupVolunteerCounter: function () {
        const input = document.getElementById('inputRequiredVolunteers');
        const btnMinus = document.getElementById('btnVolunteerMinus');
        const btnPlus = document.getElementById('btnVolunteerPlus');
        const display = document.getElementById('displayVolunteerCount');

        if (!input) return;

        const updateVal = (val) => {
            let num = parseInt(val, 10);
            if (isNaN(num)) num = 5;
            if (num < 1) num = 1;
            if (num > 50) num = 50;

            input.value = num;
            if (display) display.innerText = num + ' Kişi';
        };

        if (btnMinus) {
            btnMinus.addEventListener('click', () => {
                updateVal(parseInt(input.value || 5, 10) - 1);
            });
        }

        if (btnPlus) {
            btnPlus.addEventListener('click', () => {
                updateVal(parseInt(input.value || 5, 10) + 1);
            });
        }

        input.addEventListener('input', () => {
            updateVal(input.value);
        });
    },

    // 14. YARDIMCI METOTLAR
    scrollToCard: function (eventId) {
        const card = document.getElementById('event-card-' + eventId);
        if (card) {
            card.scrollIntoView({ behavior: 'smooth', block: 'center' });
            card.classList.add('ring-4', 'ring-emerald-500', 'ring-offset-2');
            setTimeout(() => {
                card.classList.remove('ring-4', 'ring-emerald-500', 'ring-offset-2');
            }, 1800);
        }
    },

    updateStatsCounters: function (events) {
        const openCount = events.filter(e => !e.isQuotaFull && e.status === 0).length;
        const totalCount = events.length;

        const openBadge = document.getElementById('statOpenCount');
        if (openBadge) openBadge.innerText = openCount;

        const totalBadge = document.getElementById('statTotalCount');
        if (totalBadge) totalBadge.innerText = totalCount;
    },

    escapeHtml: function (str) {
        if (!str) return '';
        return str
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }
};

// Sayfa yüklendiğinde başlat
document.addEventListener('DOMContentLoaded', () => {
    KampusImeceApp.init();
});
