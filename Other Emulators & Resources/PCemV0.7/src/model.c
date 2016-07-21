#include "ibm.h"
#include "cpu.h"
#include "model.h"
#include "io.h"

#include "acer386sx.h"
#include "ali1429.h"
#include "amstrad.h"
#include "dma.h"
#include "fdc.h"
#include "headland.h"
#include "ide.h"
#include "jim.h"
#include "keyboard_xt.h"
#include "keyboard_at.h"
#include "keyboard_olim24.h"
#include "lpt.h"
#include "mouse_ps2.h"
#include "mouse_serial.h"
#include "neat.h"
#include "nvr.h"
#include "olivetti_m24.h"
#include "pci.h"
#include "pic.h"
#include "pit.h"
#include "psg.h"
#include "serial.h"
#include "um8881f.h"
#include "wd76c10.h"
#include "xtide.h"

void           xt_init();
void      tandy1k_init();
void          ams_init();
void       europc_init();
void       olim24_init();
void           at_init();
void      at_neat_init();
void at_acer386sx_init();
void   at_wd76c10_init();
void   at_ali1429_init();
void  at_headland_init();
void   at_um8881f_init();

int model;

MODEL models[] =
{
        {"IBM PC",              ROM_IBMPC,     { "",      cpus_8088,  "",    NULL,       "",      NULL},         0,      xt_init},
        {"IBM XT",              ROM_IBMXT,     { "",      cpus_8088,  "",    NULL,       "",      NULL},         0,      xt_init},
        {"Generic XT clone",    ROM_GENXT,     { "",      cpus_8088,  "",    NULL,       "",      NULL},         0,      xt_init},
        {"DTK XT clone",        ROM_DTKXT,     { "",      cpus_8088,  "",    NULL,       "",      NULL},         0,      xt_init},        
        {"Tandy 1000",          ROM_TANDY,     { "",      cpus_8088,  "",    NULL,       "",      NULL},         1, tandy1k_init},
        {"Amstrad PC1512",      ROM_PC1512,    { "",      cpus_pc1512,"",    NULL,       "",      NULL},         1,     ams_init},
        {"Sinclair PC200",      ROM_PC200,     { "",      cpus_8086,  "",    NULL,       "",      NULL},         1,     ams_init},
        {"Euro PC",             ROM_EUROPC,    { "",      cpus_8086,  "",    NULL,       "",      NULL},         0,  europc_init},
        {"Olivetti M24",        ROM_OLIM24,    { "",      cpus_8086,  "",    NULL,       "",      NULL},         1,  olim24_init},        
        {"Amstrad PC1640",      ROM_PC1640,    { "",      cpus_8086,  "",    NULL,       "",      NULL},         1,     ams_init},
        {"Amstrad PC2086",      ROM_PC2086,    { "",      cpus_8086,  "",    NULL,       "",      NULL},         1,     ams_init},        
        {"Amstrad PC3086",      ROM_PC3086,    { "",      cpus_8086,  "",    NULL,       "",      NULL},         1,     ams_init},
        {"IBM AT",              ROM_IBMAT,     { "",      cpus_ibmat, "",    NULL,       "",      NULL},         0,      at_init},
        {"Commodore PC 30 III", ROM_CMDPC30,   { "",      cpus_286,   "",    NULL,       "",      NULL},         0,      at_init},        
        {"AMI 286 clone",       ROM_AMI286,    { "",      cpus_286,   "",    NULL,       "",      NULL},         0,     at_neat_init},        
        {"DELL System 200",     ROM_DELL200,   { "",      cpus_286,   "",    NULL,       "",      NULL},         0,      at_init},
        {"Acer 386SX25/N",      ROM_ACER386,   { "Intel", cpus_acer,  "",    NULL,       "",      NULL},         1, at_acer386sx_init},
        {"Amstrad MegaPC",      ROM_MEGAPC,    { "Intel", cpus_i386,  "AMD", cpus_Am386, "Cyrix", cpus_486SDLC}, 1,   at_wd76c10_init},
        {"AMI 386 clone",       ROM_AMI386,    { "Intel", cpus_i386,  "AMD", cpus_Am386, "Cyrix", cpus_486SDLC}, 0,  at_headland_init},
        {"AMI 486 clone",       ROM_AMI486,    { "Intel", cpus_i486,  "AMD", cpus_Am486, "Cyrix", cpus_Cx486},   0,   at_ali1429_init},
        {"AMI WinBIOS 486",     ROM_WIN486,    { "Intel", cpus_i486,  "AMD", cpus_Am486, "Cyrix", cpus_Cx486},   0,   at_ali1429_init},
        {"AMI WinBIOS 486 PCI", ROM_PCI486,    { "Intel", cpus_i486,  "AMD", cpus_Am486, "Cyrix", cpus_Cx486},   0,   at_um8881f_init},
        {"", -1, {"", 0, "", 0, "", 0}, 0}
};

int model_getromset()
{
        return models[model].id;
}

char *model_getname()
{
        return models[model].name;
}

void common_init()
{
        dma_init();
        fdc_init();
        lpt_init();
        pic_init();
        pit_init();
        serial1_init(0x3f8);
        serial2_init(0x2f8);
}

void xt_init()
{
        common_init();
        keyboard_xt_init();
        mouse_serial_init();
        xtide_init();
}

void tandy1k_init()
{
        common_init();
        keyboard_xt_init();
        mouse_serial_init();
        psg_init();
        xtide_init();
}

void ams_init()
{
        common_init();
        amstrad_init();
        keyboard_amstrad_init();
        nvr_init();
        xtide_init();
}

void europc_init()
{
        common_init();
        jim_init();
        keyboard_xt_init();
        mouse_serial_init();
        xtide_init();
}

void olim24_init()
{
        common_init();
        keyboard_olim24_init();
        nvr_init();
        olivetti_m24_init();
        xtide_init();
}

void at_init()
{
        common_init();
        dma16_init();
        ide_init();
        keyboard_at_init();
        if (models[model].init == at_init)
           mouse_serial_init();
        nvr_init();
        pic2_init();
}

void at_neat_init()
{
        at_init();
        mouse_serial_init();
        neat_init();
}

void at_acer386sx_init()
{
        at_init();
        mouse_ps2_init();
        acer386sx_init();
}

void at_wd76c10_init()
{
        at_init();
        mouse_ps2_init();
        wd76c10_init();
}

void at_headland_init()
{
        at_init();
        headland_init();
        mouse_serial_init();
}

void at_ali1429_init()
{
        at_init();
        ali1429_init();
        mouse_serial_init();
}

void at_um8881f_init()
{
        at_init();
        mouse_serial_init();
        pci_init();
        um8881f_init();
}

void model_init()
{
        pclog("Initting as %s\n", model_getname());
        io_init();
        
        models[model].init();
}
