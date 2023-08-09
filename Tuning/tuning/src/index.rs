pub const IDX_MATERIAL_MG: usize = 0;
pub const SIZE_MATERIAL_MG: usize = IDX_MATERIAL_MG + 5;

pub const IDX_MATERIAL_EG: usize = SIZE_MATERIAL_MG;
pub const SIZE_MATERIAL_EG: usize = IDX_MATERIAL_EG + 5;

pub const IDX_PSQT_MG: usize = SIZE_MATERIAL_EG;
pub const SIZE_PSQT_MG: usize = IDX_PSQT_MG + 64 * 6;

pub const IDX_PSQT_EG: usize = SIZE_PSQT_MG;
pub const SIZE_PSQT_EG: usize = IDX_PSQT_EG + 64 * 6;

pub const SIZE_FEATURES: usize = SIZE_PSQT_EG;
